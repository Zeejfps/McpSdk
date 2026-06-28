# Implementation Plan — Migrate to the `IContext` DI Builder API

Target: implement the public API described in [`API_DESIGN.md`](./API_DESIGN.md).

This plan turns the **current** `WithX` fluent builders into the **target** API where the builder is
constructed directly with required `(name, version)` ctor params and everything else (including the
transport) is a `Context.AddX(...)` registration on a small dependency-injection container.

---

## 1. Where we are vs. where we're going

### Current state (main tree, branch `improved_di`)
- `new ServerBuilder()` / `new ClientBuilder()` with fluent **`WithX`** methods (`WithName`, `WithVersion`,
  `WithTransport`, `WithToolsCapability`, …). No `IContext`, no DI.
- Serializer (`IJson`) is passed **by hand** into each transport extension
  (`WithStdioTransport(builder, json)`), wrapped in an `ITransportFactory`, and `Build()` calls
  `factory.Create(loggerFactory)`.
- Capabilities are single controller instances set via `WithXCapability(controller)`.
- **Tools** have no lambda builder — `WithDefaultToolsCapability(json, ctrl => ctrl.AddTool(...))`
  configures a `DefaultToolsController(json)` directly; `PageSize` is a settable property; a `null`
  tools controller suppresses the advertised tools capability (`McpServer.cs:91`).
- **HTTP server already runs many concurrent sessions**: `StreamableHttpListener` (in
  `Adapter.StreamableHttpServer`) creates a per-connection `HttpServerTransport` and invokes an
  `onSession(transport)` callback where the app builds + starts one `McpServer` per connection. There
  is **no DI scope**. Two test suites deliberately drive the listener with a **raw `RequestReceived`
  handler and no `McpServer`** (`Server.Tests/StreamableHttpTransportTests.cs`).
- `IServer` = `Start()/Stop()/Log()`. `IClient` = `Connect()/…`. The concrete request handler is the
  `McpServer : IServer` class (`Server/McpServer.cs`); the concrete client is `McpClient : IClient`
  (`Client/McpClient.cs`). **These names are kept** (no static factory competes for them). The
  `McpServer` ctor takes `ITransport, ServerInfo, ILoggerFactory, IToolsController, IPromptController,
  IResourcesController, ICompletionController = null, bool loggingEnabled = false` — i.e. **optional
  controllers + a `bool`** (relevant to activation, decision #3).
- Client capabilities go through a `IXCapabilityFactory.Create()` indirection.
- **No DI anywhere in the main tree.**

### Asset we can reuse
`.claude/worktrees/di-context/Shared/` holds a **complete, working flat DI container** to port:
- `IContext`, `DiContainer`, `ServiceProvider` (reflection ctor injection, eager singletons, last-wins,
  cycle detection), `ServiceDescriptor` + `ServiceLifetime{Singleton,Transient}`,
  `ContextRegistrationExtensions`, `ServiceProviderExtensions`.
- Also present: `IOptions`/`OptionsContextExtensions` and `LoggingContextExtensions` (reuse for
  `ConfigureInfo`/logging if compatible, else adapt).
- `Server.Tests/Conformance/DiContainerTests.cs` — a test set, but written as a `static partial class
  ConformanceTests` with its own static `Assert`; it needs a **structural rewrite** into a
  `ConformanceSuite` subclass to fit the live harness.
- Adapter/capability context extensions — **shape references only** (they target the older SSE
  transports + older builder); re-author against the current tree.

**Gaps the ported container has** (all become explicit tasks): it is **flat / single-scope**; it has
**no multi-registration / `GetServices<T>()`**; it **cannot activate unregistered concrete types**
(needed by `AddTool<T>()`); and it caches singletons **per service type**, not per descriptor.

### Target state (from `API_DESIGN.md`)
- `new ServerBuilder(name, version)` / `new ClientBuilder(name, version)` — required ctor params, **no
  static `CreateBuilder` factory**.
- Thin `ServerBuilder` / `ClientBuilder`: expose `IContext Context { get; }` + synchronous `Build()`.
- Transport is a registration. Server transport registers an **`IServerHost`** (lifecycle owner);
  `Build()` resolves the single `IServerHost`, returns it as `IServer`; **no host ⇒ `Build()` throws**.
- `Build()` validates wiring: **missing serializer OR missing transport throws at `Build()`**, not later.
  `Start()/Connect()` are async (I/O); `Build()` is sync.
- `ConfigureInfo(...)` for title/description; name/version are the builder ctor params.
- Shared registrations: `AddNewtonsoftJson()`/`AddSystemTextJson()`/`AddConsoleLogger()`/`AddLogger(...)`.
- Every server capability is `Add<Name>Capability(...)`; tools also gain a **lambda `IToolsBuilder`**
  (`AddTool(handler)`, `AddTool<THandler>()` container-constructed, `WithPageSize(n)`).
- HTTP server: `AddStreamableHttpTransport(baseUrl, path, http => http.ConfigureSession(session => …))`,
  where `session.Context` is a **per-session child container** and `ISession` exposes
  `SessionId`/`Origin`/`Transport`.
- Client capabilities take a controller directly: `AddRootsCapability(IRootsController)` etc.
- Per the project's standing decision: **full replacement** — delete the `WithX` service API, no shims.

### The gate (acceptance for every task)
`dotnet run --project Server.Tests -- conformance` (~230 assertions, 13+ suites). Container work adds a
ported `DiContainerTests` suite. **Keep the repo compiling and the gate green after every task** — see
the dual-mode `Build()` strategy (decision #5) that makes this true through the cutover.

---

## 2. Design decisions (defaults chosen so tasks are actionable)

1. **Entry point: `new ServerBuilder(name, version)` (no static factory).** Required constructor
   parameters give a compile-time guarantee that name and version are always supplied, and using a plain
   constructor means the public `McpServer`/`McpClient` symbols never collide with the concrete runtime
   classes of those names — so **those classes keep their names; no rename.** The trade-off is giving up
   an ASP.NET-style `CreateBuilder` factory and `McpServer.`-dot discoverability. (Renaming the concrete
   `McpServer`→`McpServerSession` for host-vs-session clarity is optional and out of scope here.)

2. **Per-session tools must *aggregate*, not *replace*** (HTTP client sees shared `EchoTool` **plus**
   session `CartTool`/`AdminTool`). Add **multi-registration + `GetServices<T>()`** to the container.
   Register each tools controller (leaf) under an **internal marker type** (e.g. `IToolsControllerSource`),
   and register the **public `IToolsController` as a `CompositeToolsController`** that merges all leaves
   resolved via `GetServices` (child overlaid on root). **Suppress the tools capability entirely when
   zero leaves exist** (preserve today's null-controller behavior). This avoids the last-wins collision
   and self-recursion of registering leaves and composite under the same type.

3. **`McpServer` (the session server) activation is factory-based, not type-injected.** Its ctor has
   optional controllers + a `bool`, which the reflection container cannot satisfy by type. Register it
   via a **factory delegate** that reads `ServerInfo`/`loggingEnabled` from options and pulls controllers
   with null-tolerant `GetService<T>()` (T9). This is load-bearing for every host.

4. **`ITransportFactory` is removed.** The target has no factory. Transport registrations register the
   `ITransport` (client) or `IServerHost` (server) **directly**, resolving `IJson`/`ILoggerFactory` from
   the container. `ITransportFactory`/`StdioTransportFactory`/client factories are deleted in T18.

5. **Dual-mode `Build()` keeps the gate green during cutover.** While both APIs coexist (T7–T17),
   `ServerBuilder.Build()` resolves `IServerHost` **if any host is registered in `Context`**, else falls
   back to the legacy `WithX` field path; likewise `ClientBuilder.Build()`. T18 deletes the legacy branch
   together with the `WithX` API.

6. **`IServer.Log` on a multi-session HTTP host.** `IServerHost : IServer`; the **stdio** host delegates
   `Log` to its single session; the **HTTP** host's `Log` throws `NotSupportedException` (logging is
   advertised + served *per session*). Document as a deviation in T19.

7. **`AddTool<THandler>()` activation scope.** The handler is **activated once per session scope**
   (resolved from the session's child provider via `ActivatorUtilities`), matching tool lifetime to the
   connection. Stateless tools registered as instances are shared as today.

8. **No `HttpServerTransport` relocation.** Dependency direction is already correct (adapter→core); the
   "core mustn't depend on HTTP adapters" rule is met by keeping `AddStreamableHttpTransport` in the
   adapter. `StreamableHttpListener` stays **public and raw-drivable** for the two no-server test suites.

9. **Plan artifact** is uncommitted unless asked (honoring `never-commit-without-explicit-permission`).

---

## 3. Milestones & task graph

```
M0 Container foundation ─► M1 Shared regs + ─► M2 Server pipeline ─► M4 Cutover, delete WithX, docs
   (Shared)                 builder skeleton     M3 Client pipeline
```

Each task: **Scope / Files / Acceptance**, sized to one focused PR. "Gate" = the conformance command
stays green. Standing instruction for every delegated task: *implement the scope, keep the repo
compiling, keep the gate green, add DI assertions where noted.*

---

### Milestone 0 — Container foundation (`Shared`)

**T1. Port the DI container core + options/logging infra into `Shared`.**
- *Depends on:* —. *Scope:* Copy the container files + `IOptions`/`OptionsContextExtensions`/
  `LoggingContextExtensions` from the worktree into live `Shared` (namespace `McpSdk.Shared`), no
  behavior change, nothing consumes them yet. *Files (new):* `Shared/IContext.cs`, `DiContainer.cs`,
  `ServiceProvider.cs`, `ServiceDescriptor.cs`, `ContextRegistrationExtensions.cs`,
  `ServiceProviderExtensions.cs`, `IOptions.cs`, `OptionsContextExtensions.cs`,
  `LoggingContextExtensions.cs`. *Acceptance:* compiles; Gate unchanged.

**T2. Port the container test set as a `ConformanceSuite` subclass.**
- *Depends on:* T1. *Scope:* Rewrite the worktree `DiContainerTests` (static class) into
  `DiContainerTests : ConformanceSuite` and add it to the `suites[]` array in `ConformanceTests.RunAll()`.
- *Files:* `Server.Tests/Conformance/DiContainerTests.cs` (new), `Server.Tests/ConformanceTests.cs` (edit).
- *Acceptance:* Gate green with the DI assertions reported.

**T3. Multi-registration + `GetServices<T>()` + per-descriptor singleton cache.**
- *Depends on:* T1. *Scope:* Retain **all** descriptors per service type (ordered). `GetService` keeps
  **last-wins**; add `IEnumerable<object> GetServices(Type)` + `GetServices<T>()` returning every
  registration in order. Re-key the singleton cache **per `ServiceDescriptor`** (not per `Type`) and
  rework eager realization so each singleton descriptor realizes its own instance. *Files:*
  `Shared/ServiceProvider.cs`, `DiContainer.cs`, `ServiceProviderExtensions.cs`, `DiContainerTests`
  (register two `IFoo`: `GetService`→last, `GetServices`→both in order). *Acceptance:* new assertions; Gate green.

**T4. Child/scoped container.** *(sequence after T3 — same `ServiceProvider` internals)*
- *Depends on:* T3. *Scope:* Derive a **child provider** from a root `ServiceProvider` + child
  `ServiceDescriptor`s. A type registered in the child resolves from the child (child **eagerly realizes
  its own singletons** to stay read-only/thread-safe); otherwise delegate to the parent (shared root
  singleton by reference). `GetServices<T>` overlays child registrations **after** parent ones. Expose
  the surface the HTTP host needs (e.g. internal `ServiceProvider.CreateChild(IEnumerable<ServiceDescriptor>)`;
  a fresh `DiContainer` serves as `session.Context`). *Files:* `Shared/ServiceProvider.cs`, `DiContainer.cs`,
  `DiContainerTests` (child override wins; parent singleton shared by reference; sibling children
  isolated; concurrent child creation safe). *Acceptance:* new child-scope assertions; Gate green.

**T5. `ActivatorUtilities.CreateInstance(IServiceProvider, Type)`.**
- *Depends on:* T1. *Scope:* Activate an **unregistered** concrete type via greediest-satisfiable-ctor
  injection (the ported container only activates *registered* types). Needed by `AddTool<T>()`. *Files:*
  `Shared/ActivatorUtilities.cs` (new), `DiContainerTests`. *Acceptance:* assertions (activate a type
  with injected params); Gate green.

---

### Milestone 1 — Shared registrations + builder skeleton

**T6. Serializer & logger context extensions.**
- *Depends on:* T1. *Scope:* `AddNewtonsoftJson`/`AddSystemTextJson` (their adapters) →
  `AddSingleton<IJson>(…)`; `AddConsoleLogger` (ConsoleLogger adapter, server/client parity);
  `AddLogger(IContext, ILoggerFactory)` (shared). Builders later seed a default
  `AddSingleton<ILoggerFactory>(NullLoggerFactory)` overridable by last-wins. Additive (not yet in
  `Build()`). *Files:* `Adapter.Newtonsoft.Json/NewtonsoftJsonContextExtensions.cs`,
  `Adapter.System.Text.Json/SystemTextJsonContextExtensions.cs`,
  `Adapter.ConsoleLogger/ConsoleLoggerContextExtensions.cs` (all new). *Acceptance:* compiles; optional
  resolve assertions; Gate green.

**T7. Builder ctors + `Context` + dual-mode `Build()` skeleton.**
- *Depends on:* T1. *Scope:* Give `ServerBuilder`/`ClientBuilder` a **required ctor `(string name,
  string version)`** and an `IContext Context { get; }` (a `DiContainer`); the ctor seeds name/version
  into `ServerInfoOptions`/`ClientInfoOptions` + a default `NullLoggerFactory`. Keep the parameterless
  ctor + `WithName`/`WithVersion` working for now (legacy path). `Build()` becomes **dual-mode**
  (decision #5): build the provider, resolve `IServerHost`/transport if registered, else legacy field
  path. **No static factory classes; concrete `McpServer`/`McpClient` keep their names.** *Files:*
  `Server/ServerBuilder.cs`, `Client/ClientBuilder.cs`. *Acceptance:* `new ServerBuilder("n","1.0").Context`
  usable; legacy tests still pass; Gate green.

**T8. `ConfigureInfo` + options shapes.**
- *Depends on:* T7 (+ reuse `IOptions` from T1). *Scope:* `ServerInfoOptions{Title,Description}` /
  `ClientInfoOptions{Title,Description}` registered as singletons; `ConfigureInfo(IContext, Action<…>)`
  mutates them; `ServerInfo`/`ClientInfo` produced at resolve time from name+version+options. *Files:*
  `Server/ServerInfoOptions.cs`, `Client/ClientInfoOptions.cs`, `Server/ServerContextExtensions.cs`,
  `Client/ClientContextExtensions.cs` (new). *Acceptance:* title/description flow into advertised info; Gate green.

---

### Milestone 2 — Server pipeline

**T9. Factory registration for the session server (`McpServer`).**
- *Depends on:* T8. *Scope:* Register the concrete `McpServer` via a **factory delegate** (decision #3):
  resolve `ITransport`, `ILoggerFactory`, build `ServerInfo` from options, read `loggingEnabled` from the
  logging-capability marker, and pull `IToolsController`/`IPromptController`/`IResourcesController`/
  `ICompletionController` with null-tolerant `GetService<T>()`. *Files:* `Server/ServerSessionFactory.cs`
  (new) or a registration helper. *Acceptance:* an `McpServer` resolves from a provider with only a
  transport + serializer + tools registered; Gate green.

**T10. `IServerHost` + stdio host + `AddStdioTransport()` (server).**
- *Depends on:* T6, T7, T9. *Scope:* `IServerHost : IServer`. `StdioServerHost` resolves `IJson`/
  `ILoggerFactory` in its **ctor** (so a missing serializer fails when the provider is built inside
  `Build()` — satisfies the Build-validates-serializer rule), builds a `StdioTransport`, resolves an
  `McpServer`, and on `Start()` pumps. `AddStdioTransport(this IContext)` registers `IServerHost`
  directly (no `ITransportFactory`). `ServerBuilder.Build()` resolves the host; **throws a clear "no
  transport registered" error** if absent. *Files:* `Server/IServerHost.cs`, `Server/StdioServerHost.cs`,
  `Server/StdioServerTransportExtensions.cs` (new), `Server/ServerBuilder.cs`, `Server/StdioTransport.cs`
  (resolve `IJson` from DI). *Acceptance:* a stdio server built via the new API passes the stdio suite;
  building with a transport but no serializer throws at `Build()`; Gate green.

**T11. Tools capability: `IToolsBuilder` + composite + controller overload.**
- *Depends on:* T3, T5, T7. *Scope:* `IToolsBuilder` (`AddTool(IToolHandler)`, `AddTool<THandler>()`,
  `WithPageSize(int)`); `AddToolsCapability(IContext, Action<IToolsBuilder>)` and
  `AddToolsCapability(IContext, IToolsController)`. Each registers a leaf controller under the internal
  marker type; the public `IToolsController` is a `CompositeToolsController` merging
  `GetServices<leaf>()` (decision #2), **suppressed when zero leaves**. `AddTool<T>()` activates the
  handler via `ActivatorUtilities` at session scope (decision #7); `DefaultToolsController(json)` resolves
  `IJson` from DI. *Files:* `Server/IToolsBuilder.cs`, `ToolsBuilder.cs`, `CompositeToolsController.cs`
  (new), `Server/ServerContextExtensions.cs` (tools overloads), `Server/DefaultToolsController.cs` (expose
  enumeration if the composite needs it). *Acceptance:* tools + pagination suites pass via the new API; an
  `AddTool<T>()` with a ctor dependency resolves; zero-tools server doesn't advertise tools; Gate green.

**T12. Remaining server capabilities as `Add<Name>Capability`.**
- *Depends on:* T7. *Scope:* `AddPromptsCapability(IPromptController)`,
  `AddResourcesCapability(IResourcesController)`, `AddCompletionCapability(ICompletionController)`,
  `AddLoggingCapability()` (carry `loggingEnabled` via a `LoggingCapabilityOptions` singleton the session
  factory reads in T9). *Files:* `Server/ServerContextExtensions.cs` (extend),
  `Server/LoggingCapabilityOptions.cs` (new). *Acceptance:* prompts/resources/completion/logging suites
  pass via the new API; Gate green.

**T13a. Streamable HTTP host skeleton (single-session, no child scope).**
- *Depends on:* T9, T10. *Scope:* In `Adapter.StreamableHttpServer`, `StreamableHttpServerHost :
  IServerHost` owning the existing `StreamableHttpListener`; `IStreamableHttpServerOptions` (with
  `ConfigureSession`), `ISession` (`Context`/`SessionId`/`Origin`/`Transport`);
  `AddStreamableHttpTransport(IContext, baseUrl, path, Action<IStreamableHttpServerOptions> configure=null)`
  registers the host as `IServerHost`. For now each connection resolves the session server from the
  **root** provider (no per-session adds yet). Keep the listener public for the two raw-handler suites.
  *Files:* `Adapter.StreamableHttpServer/StreamableHttpServerHost.cs`,
  `StreamableHttpServerTransportExtensions.cs`, `IStreamableHttpServerOptions.cs`, `ISession.cs`,
  `Session.cs` (new). *Acceptance:* the StreamableHttp conformance suite passes via the new API (no
  per-session tools yet); Gate green.

**T13b. Per-session child scope + `ConfigureSession` + aggregation.**
- *Depends on:* T4, T11, T13a. *Scope:* Per connection, create a child container off the root, register
  the session's `ITransport` into it, build `ISession` with `Context`=child, run `ConfigureSession`
  against `session.Context`, resolve `McpServer` from the child, start it. Tools aggregate
  (root + session) via the composite + `GetServices` overlay. *Files:* `StreamableHttpServerHost.cs`,
  `Session.cs`. *Acceptance:* a 2-session test shows shared `EchoTool` + per-session `CartTool`
  (and `AdminTool` only for the admin origin), sibling sessions isolated; Gate green.

---

### Milestone 3 — Client pipeline

**T14. Client transports as registrations.**
- *Depends on:* T6, T7. *Scope:* `AddStdioTransport(IContext, string command, params string[] args)`
  (core `McpSdk.Client`) and `AddStreamableHttpTransport(IContext, string endpointUrl)`
  (`Adapter.StreamableHttpClient`) register the connection `ITransport` **directly** (resolve `IJson`/
  `ILoggerFactory` from DI; no client `ITransportFactory`). *Files:*
  `Client/StdioClientTransportExtensions.cs`,
  `Adapter.StreamableHttpClient/StreamableHttpClientTransportExtensions.cs` (new),
  `Client/Transports/*` (resolve `IJson` from DI). *Acceptance:* a client transport resolves from the
  container; Gate green.

**T15. Client capabilities by controller + DI-driven `Build()`.**
- *Depends on:* T7. *Scope:* `AddRootsCapability(IRootsController)`,
  `AddSamplingCapability(ISamplingController)`, `AddElicitationCapability(IElicitationController)` register
  controllers directly (drop/bypass the `IXCapabilityFactory` indirection). `ClientBuilder.Build()`
  (dual-mode) resolves `ITransport` (**throw if none** — Build validation parity),
  `ILoggerFactory`, `ClientInfo`, optional controllers → `McpClient`. *Files:*
  `Client/ClientContextExtensions.cs`, `Client/ClientBuilder.cs`. *Acceptance:* client builds via the
  new API; building with no transport throws at `Build()`; Gate green.

---

### Milestone 4 — Cutover, deletion, docs

**T16. Migrate Server.Tests call sites to the new API.**
- *Depends on:* M2 complete. *Scope:* Replace every `new ServerBuilder().WithX()…`
  (Program.cs, ConformanceSuite helpers, PaginationTests, ProgressTests, Stdio/StreamableHttp tests,
  CancellationTests, …) with `new ServerBuilder(name, version)` + `.Context.AddX(...)`; HTTP `onSession`
  usage → `ConfigureSession`. **The two raw-handler StreamableHttp suites keep driving
  `StreamableHttpListener` directly** (decision #8) — do not force them through `ConfigureSession`.
  *Files:* ~13 `Server.Tests/*.cs`. *Acceptance:* Gate green entirely on the new API.

**T17. Migrate Client.Tests call sites.**
- *Depends on:* M3 complete. *Files:* `Client.Tests/Program.cs` (+ factory stubs if indirection dropped).
- *Acceptance:* client tests run on the new API.

**T18. Delete the `WithX` service API + dead plumbing + legacy `Build()` branch.**
- *Depends on:* T16, T17. *Scope:* Remove all `WithX` service methods (incl. `WithName`/`WithVersion` —
  now ctor params), the parameterless builder ctors, the dual-mode legacy branch in both `Build()`s,
  `ITransportFactory` + `StdioTransportFactory` + client transport factories, and the
  `*CapabilityFactory` indirection. No `[Obsolete]` shims. *Files:* `Server/ServerBuilder.cs`,
  `Client/ClientBuilder.cs`, transport/adapter extension files, factory interfaces. *Acceptance:* no
  `WithX` service methods remain; the builders only construct via `(name, version)`; Gate green.

**T19. Docs.**
- *Depends on:* T18. *Scope:* Update `README.md` examples to the new API; document deviations from
  `API_DESIGN.md` (`IServer.Log` on HTTP host, decision #6); optionally mark `API_DESIGN.md` implemented.
- *Files:* `README.md`, `API_DESIGN.md`. *Acceptance:* docs match the shipped API.

---

## 4. Execution order & parallelism

1. **T1** unblocks all.
2. After T1, in parallel: **T2**, **T3 → T4** (sequential, same internals), **T5**, **T6**, **T7**.
3. **T8** after T7.
4. **T9** after T8 → **T10**; **T11** (after T3+T5+T7), **T12** (after T7) in parallel.
5. **T13a** after T9+T10 → **T13b** after T4+T11+T13a.
6. **T14**/**T15** (client) after T6/T7 — overlap M2.
7. **T16** after M2, **T17** after M3, then **T18** (delete), then **T19** (docs).

## 5. Risks / watch-items
- **Gate-green through cutover** rests entirely on the dual-mode `Build()` (decision #5); verify legacy
  tests still pass after T7 before proceeding.
- **Tools aggregation** (decision #2) is the subtlest piece — the 2-session HTTP test (T13b) is the
  proof: shared + per-session tools both visible, siblings isolated, zero-tools server silent.
- **Session-server activation** must be factory-based (decision #3); a typed registration will throw
  (optional controllers + `bool` ctor param).
- **Build()-time validation** of serializer relies on the host ctor resolving `IJson` + eager singleton
  realization; confirm the failure surfaces at `Build()`, not `Start()`.
- **Child container thread-safety**: children must eagerly realize their own singletons (read-only after
  construction) since HTTP creates/resolves them concurrently.
- **Core must not depend on HTTP adapters** — both `AddStreamableHttpTransport` overloads live in adapter
  assemblies; no `HttpServerTransport` relocation needed (decision #8).
- **`Build()` sync, `Start()/Connect()` async** — keep all I/O out of `Build()`.
