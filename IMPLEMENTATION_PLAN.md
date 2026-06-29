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
- Serializer (`IJson`) is passed **by hand** into each transport extension, wrapped in an
  `ITransportFactory`, and `Build()` calls `factory.Create(loggerFactory)`. The live transport
  extensions are: server `WithStdioTransport(this ServerBuilder, IJson)` (`Server/StdioTransport.cs:80`
  → `StdioTransportFactory(IJson)`) and `WithStreamableHttpTransport(this ServerBuilder, ITransport)`
  (`Server/HttpServerTransport.cs:228`, wrapping the listener's per-session transport via an internal
  `ExistingTransportFactory`); client `WithStdioTransport(this ClientBuilder, IJson, string command,
  string[] args)` (`Client/Transports/StdioTransport.cs:143`) and `WithStreamableHttpTransport(this
  ClientBuilder, IJson, IStreamableHttpClient)` (`Client/Transports/StreamableHttpTransport.cs:146`).
  Tests build over an **in-memory** transport via `FixedTransportFactory(ITransport)`
  (`Server.Tests/FixedTransportFactory.cs`) — see the in-memory-host gap below.
- Capabilities are single controller instances set via `WithXCapability(controller)`.
- **Tools** have no lambda builder — `WithDefaultToolsCapability(json, ctrl => ctrl.AddTool(...))`
  configures a `DefaultToolsController(json)` directly; `PageSize` is a settable property; a `null`
  tools controller suppresses the advertised tools capability (`McpServer.cs:91`).
- **HTTP server already runs many concurrent sessions**: `StreamableHttpListener` (in
  `Adapter.StreamableHttpServer`, ctor `(string baseUrl, string endpointPath, IJson json, ILoggerFactory
  loggerFactory, Func<ITransport,Task> onSession, IEnumerable<string> allowedOrigins = null)`) creates a
  per-connection `HttpServerTransport` (fresh GUID session id) and invokes the `onSession(transport)`
  callback where the app builds + starts one `McpServer` per connection. There is **no DI scope**.
  Within the **single** `StreamableHttpTransportTests` suite, **two test methods**
  (`StreamableHttpServerToClient`, `StreamableHttpResumability`) deliberately drive the listener with a
  **raw `RequestReceived` handler and no `McpServer`** — they answer `initialize` at the transport level
  and call `transport.Start()` directly (`Server.Tests/StreamableHttpTransportTests.cs`).
- `IServer` = `Start()/Stop()/Log(LoggingLevel, Json, string=null)`. `IClient` = `Connect()/Ping/
  SetLoggingLevel/ListTools/CallTool` + events. The concrete request handler is the **`internal sealed`
  `McpServer : IServer`** (`Server/McpServer.cs`); the concrete client is the **`internal sealed`
  `McpClient : IClient`** (`Client/McpClient.cs`) — neither is a public symbol today. **These names are
  kept** (decision #1). The `McpServer` ctor takes `ITransport, ServerInfo, ILoggerFactory,
  IToolsController, IPromptController, IResourcesController, ICompletionController = null, bool
  loggingEnabled = false` — i.e. **optional controllers + a `bool`** (relevant to activation, decision
  #3). The `McpClient` ctor takes `ITransport, ILoggerFactory, ClientInfo, IRootsController,
  ISamplingController, IElicitationController` — six **required** params, but the three controllers are
  passed `null` when absent, so the reflection container can't type-inject it either (decision #3 applies
  to the client too — see T15).
- **`Build()` transport validation is asymmetric today**: `ClientBuilder.Build()` throws if the
  transport is null (`Client/ClientBuilder.cs:90`), but `ServerBuilder.Build()` does **not** — it calls
  `_transportFactory.Create(...)` on a null factory and `NullReferenceException`s
  (`Server/ServerBuilder.cs:105`). The target makes both throw a clear "no transport registered" error
  at `Build()`.
- Client capabilities go through a `IXCapabilityFactory.Create()` indirection.
- **No DI anywhere in the main tree.**

### Asset we can reuse
`.claude/worktrees/di-context/Shared/` holds a **complete, working flat DI container** to port:
- `IContext`, `DiContainer`, `ServiceProvider` (reflection ctor injection, eager singletons, last-wins,
  cycle detection), `ServiceDescriptor` + `ServiceLifetime{Singleton,Transient}`,
  `ContextRegistrationExtensions`, `ServiceProviderExtensions`.
- Also present: `IOptions`/`OptionsContextExtensions` and `LoggingContextExtensions` (reuse for
  `ConfigureInfo`/logging if compatible, else adapt).
- `Server.Tests/Conformance/DiContainerTests.cs` — a test set written as a `static partial class
  ConformanceTests` with its own static `Assert` plus a static `Throws<TException>(Action, string)`
  helper. It holds **11 test bodies** (instance / factory / reflection-ctor resolution,
  singleton-vs-transient, last-registration-wins, provider-resolves-itself,
  GetService-null/GetRequired-throws, circular-dependency, ambiguous-ctor, and
  `Configure<T>`/`IOptions<T>`) plus **8 private fixture types**
  (`Alpha`/`Beta`/`Counter`/`SampleOptions`/`CycleA`/`CycleB`/`Ambiguous` + their interfaces). The
  `OptionsConfigureAndResolve` body depends on the `Configure<T>`/`IOptions<T>` infra ported in T1. It
  needs a **structural rewrite** into `DiContainerTests : ConformanceSuite` (add a `Title` + a `Run()`
  that awaits each body, map the static `Assert` onto the base `Assert`, and keep `Throws<T>` as a local
  helper) to fit the live harness.
- Adapter/capability context extensions — **shape references only** (they target the older SSE
  transports + older builder); re-author against the current tree.

**Gaps the ported container has** (all become explicit tasks): it is **flat / single-scope**
(`ServiceProvider` holds one `_descriptors` dict + one `_singletons` dict, keyed by `Type`); it has
**no multi-registration / `GetServices<T>()`** (`_descriptors[serviceType] = descriptor` overwrites —
last-wins); it **cannot activate unregistered concrete types** (`Resolve` throws / returns null for an
unregistered type — needed by `AddTool<T>()`); and it caches singletons **per service type**, not per
descriptor. (It *does* already select the greediest-satisfiable public constructor —
`ServiceProvider.SelectConstructor`/`IsSatisfiable`, treating `IServiceProvider` as always-available —
so T5's `ActivatorUtilities` is a refactor that applies that same rule to an *unregistered* type given a
provider, not a from-scratch algorithm.)

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
`dotnet run --project Server.Tests -- conformance` — today **18 `ConformanceSuite` subclasses / 239
`Assert()` calls**, built as an inline `suites[]` array in `ConformanceTests.RunAll()`
(`Server.Tests/ConformanceTests.cs:20-53`) and dispatched from `Program.cs` (`args[0] ==
"conformance"`). Container work adds a ported `DiContainerTests` suite (the 19th). **Keep the repo
compiling and the gate green after every task** — see the dual-mode `Build()` strategy (decision #5)
that makes this true through the cutover.

---

## 2. Design decisions (defaults chosen so tasks are actionable)

1. **Entry point: `new ServerBuilder(name, version)` (no static factory).** Required constructor
   parameters give a compile-time guarantee that name and version are always supplied. The concrete
   `McpServer`/`McpClient` are `internal sealed` and **not public symbols today**; an ASP.NET-style
   `McpServer.CreateBuilder(...)` static factory would force promoting `McpServer` to a public type
   (colliding with the runtime class of that name), whereas a plain builder ctor needs no public
   `McpServer`/`McpClient` symbol at all — so **those classes keep their names; no rename** (the
   rationale stated in `API_DESIGN.md`). The trade-off is giving up `CreateBuilder`/`McpServer.`-dot
   discoverability. (Renaming the concrete `McpServer`→`McpServerSession` for host-vs-session clarity is
   optional and out of scope here.)

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
   adapter. `StreamableHttpListener` stays **public and raw-drivable** for the two no-server *test
   methods* (`StreamableHttpServerToClient`/`StreamableHttpResumability` in `StreamableHttpTransportTests`).

9. **Plan artifact** is uncommitted unless asked (honoring `never-commit-without-explicit-permission`).

10. **An in-memory server host is needed for the conformance suites (new task T10b).** ~12 of the 18
    suites build a server over an in-memory transport pair via `FixedTransportFactory(InMemoryTransport)`
    (`ConformanceSuite.BuildServer`, `CancellationTests`, `Completion`, `Logging`, `Pagination`,
    `Progress`, `Prompts`, `Resources`, `Tools`, …). The new `Build()` resolves an `IServerHost`, but
    there is **no public transport for a pre-built in-memory `ITransport`**. So the cutover needs an
    internal/test-only host that owns a supplied `ITransport` and builds one session `McpServer` from the
    container (the generic core of `StdioServerHost`; stdio just *creates* the transport, in-memory
    *receives* it). The **client** side needs no host — `ClientBuilder.Build()` resolves the single
    `ITransport` directly — so test clients just register the in-memory `ITransport`.

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
- *Depends on:* T1. *Scope:* Rewrite the worktree `DiContainerTests` (a `static partial class
  ConformanceTests` with 11 test bodies, a static `Throws<T>`, and 8 private fixtures) into a
  `DiContainerTests : ConformanceSuite` (override `Title`; `Run()` awaits each body wrapped in `Test(name,
  …)`; map the static `Assert` onto the base `protected Assert`; keep `Throws<T>` as a private helper).
  The fixtures move with it. Then add `new DiContainerTests(report)` to the `suites[]` array in
  `ConformanceTests.RunAll()` (`Server.Tests/ConformanceTests.cs:20-53`). The `OptionsConfigureAndResolve`
  body exercises `Configure<T>`/`IOptions<T>` from T1, so T1 must land first.
- *Files:* `Server.Tests/Conformance/DiContainerTests.cs` (new), `Server.Tests/ConformanceTests.cs` (edit).
- *Acceptance:* Gate green (now 19 suites) with the DI assertions reported.

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
  injection (the ported container only activates *registered* types). Needed by `AddTool<T>()`. **Reuse,
  don't reinvent**: the existing `ServiceProvider.SelectConstructor`/`IsSatisfiable` already does
  greediest-satisfiable selection (treating `IServiceProvider` as always-resolvable, erroring on
  equal-arity ties) — extract that into a shared helper both the provider and `ActivatorUtilities` call,
  so activation rules stay identical and `AddTool<T>` ties/missing-deps fail the same way. *Files:*
  `Shared/ActivatorUtilities.cs` (new), `Shared/ServiceProvider.cs` (extract selection), `DiContainerTests`.
  *Acceptance:* assertions (activate an unregistered type with injected + `IServiceProvider` params; a
  missing dep throws); Gate green.

---

### Milestone 1 — Shared registrations + builder skeleton

**T6. Serializer & logger context extensions.**
- *Depends on:* T1. *Scope:* `AddNewtonsoftJson` → `AddSingleton<IJson>(new NewtonsoftJson())` and
  `AddSystemTextJson` → `AddSingleton<IJson>(new SystemJson())` (both `public sealed : IJson`);
  `AddLogger(IContext, ILoggerFactory)` — this can be the worktree `LoggingContextExtensions.AddLogger`
  ported verbatim in T1, so T6 may not need to re-author it. **`AddConsoleLogger` has an asymmetry to
  resolve:** today only a **server** `WithConsoleLogger(this ServerBuilder)` exists
  (`Adapter.ConsoleLogger/ServerBuilderExtensions.cs` → `ServerConsoleLoggerFactory`); the client has a
  `ClientConsoleLoggerFactory` class but **no builder extension**. The two factories differ (the server
  one must avoid stdout, which the stdio transport owns). A single `AddConsoleLogger(IContext)` can't tell
  server from client — **decide**: either two namespace-scoped overloads (server `using` registers
  `ServerConsoleLoggerFactory`, client `using` registers `ClientConsoleLoggerFactory`) or pick by a
  builder-seeded marker. Builders later seed a default `AddSingleton<ILoggerFactory>(NullLoggerFactory)`
  overridable by last-wins. Additive (not yet in `Build()`). *Files:*
  `Adapter.Newtonsoft.Json/NewtonsoftJsonContextExtensions.cs`,
  `Adapter.System.Text.Json/SystemTextJsonContextExtensions.cs`,
  `Adapter.ConsoleLogger/ConsoleLoggerContextExtensions.cs` (all new). *Acceptance:* compiles; resolve
  `IJson`/`ILoggerFactory` from a built provider; Gate green.

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
- *Grounding note — where `ITransport` comes from (reconciles T9↔T10/T13b):* the `McpServer` factory
  resolves `ITransport` **from the scope it is resolved in**. So the rule is "register the session
  `ITransport` into that scope, then resolve `McpServer`": **stdio** and the **in-memory test host**
  register the transport in the **root** (one session), so `AddStdioTransport()` registers a singleton
  `ITransport` factory *and* the `IServerHost`; **HTTP** registers a *different* transport per connection,
  so it goes into the per-session **child** (T13b). The host never constructs the `McpServer` by hand.

**T10. `IServerHost` + stdio host + `AddStdioTransport()` (server).**
- *Depends on:* T6, T7, T9. *Scope:* `IServerHost : IServer`. `AddStdioTransport(this IContext)` registers
  **two** things (no `ITransportFactory`): a **singleton `ITransport`** factory `sp => new StdioTransport(
  sp.GetService<IJson>(), sp.GetService<ILoggerFactory>())`, and the **`IServerHost`** (`StdioServerHost`).
  Because the `ITransport` is a singleton it is **eagerly realized when the provider is built inside
  `Build()`** — so a missing serializer throws at `Build()` (the Build-validates-serializer rule).
  `StdioServerHost` resolves the `McpServer` (via T9's factory, which pulls that same `ITransport`) and on
  `Start()` pumps. `ServerBuilder.Build()` resolves the single `IServerHost`; **throws a clear "no
  transport registered" error** if absent. *Files:* `Server/IServerHost.cs`, `Server/StdioServerHost.cs`,
  `Server/StdioServerTransportExtensions.cs` (new), `Server/ServerBuilder.cs`, `Server/StdioTransport.cs`
  (resolve `IJson` from DI). *Acceptance:* a stdio server built via the new API passes the stdio suite
  (`StdioTests`, `Program.cs` stdio path); building with a transport but no serializer throws at
  `Build()`; Gate green.

**T10b. In-memory server host (test infrastructure for the conformance suites).**
- *Depends on:* T9, T10. *Scope:* ~12 suites build a server over an in-memory `ITransport` pair via
  `FixedTransportFactory(InMemoryTransport)` (decision #10). Provide the way to build a server over a
  **pre-built `ITransport`**: register it as a **singleton `ITransport` instance** plus the same
  single-session `IServerHost` as T10 (factor `StdioServerHost`'s pump loop into a shared host that just
  resolves `ITransport` + `McpServer`; stdio *creates* the transport, in-memory *receives* it). Surface it
  for tests — e.g. an internal `AddInMemoryServerTransport(this IContext, ITransport)` (core, `internal`
  +`InternalsVisibleTo(Server.Tests)`), or a `Server.Tests` helper. Clients need no host:
  `ClientBuilder.Build()` resolves the registered `ITransport` directly. *Files:*
  `Server/SingleSessionServerHost.cs` (extracted) or `Server/InMemoryServerTransportExtensions.cs` (new),
  `Server.Tests/ConformanceSuite.cs` (rewire `BuildServer`/`ConnectClient`). *Acceptance:* a suite's
  `BuildServer(InMemoryTransport)` produces a working server via the new API; Gate green. **This unblocks
  T16** (most suites can't migrate without it).

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

**T13a. Streamable HTTP host skeleton (per-connection scope, no `ConfigureSession` yet).**
- *Depends on:* T4, T9, T10. *(T4 is required: each connection's `HttpServerTransport` is different, so it
  cannot be a root singleton — it must be registered into a per-connection child scope for T9's factory to
  resolve it. This is the same scope mechanism T13b builds on.)* *Scope:* In `Adapter.StreamableHttpServer`,
  `StreamableHttpServerHost : IServerHost` owning the existing `StreamableHttpListener`;
  `IStreamableHttpServerOptions` (with `ConfigureSession`), `ISession`
  (`Context`/`SessionId`/`Origin`/`Transport`);
  `AddStreamableHttpTransport(IContext, baseUrl, path, Action<IStreamableHttpServerOptions> configure=null)`
  registers the host as `IServerHost`. The host passes an `onSession(transport)` to the listener; in that
  callback it creates a child scope, registers the connection `ITransport` into it, resolves `McpServer`
  from the child, and starts it — but does **not** run `ConfigureSession` or per-session tool adds yet.
  Keep the listener public for the two raw-handler *test methods*. *Files:*
  `Adapter.StreamableHttpServer/StreamableHttpServerHost.cs`, `StreamableHttpServerTransportExtensions.cs`,
  `IStreamableHttpServerOptions.cs`, `ISession.cs`, `Session.cs` (new). *Acceptance:* the StreamableHttp
  conformance suite passes via the new API (no per-session tools yet); Gate green.

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
  `ILoggerFactory` from DI; no client `ITransportFactory`). **API shift to ground:** today the client HTTP
  transport is `WithStreamableHttpTransport(ClientBuilder, IJson, IStreamableHttpClient)` — it takes a
  *pre-built* `StreamableHttpClientAdapter` (`StreamableHttpClientAdapter(target, loggerFactory)`). The
  target signature takes a bare **`endpointUrl` string** (`API_DESIGN.md` line 202), so the new
  registration must construct the `StreamableHttpClientAdapter` **internally** from the url + DI-resolved
  `ILoggerFactory`, then build the transport. Current client stdio is
  `WithStdioTransport(ClientBuilder, IJson, command, args)` (`Client/Transports/StdioTransport.cs:143`).
  *Files:* `Client/StdioClientTransportExtensions.cs`,
  `Adapter.StreamableHttpClient/StreamableHttpClientTransportExtensions.cs` (new),
  `Client/Transports/*` (resolve `IJson` from DI). *Acceptance:* a client transport resolves from the
  container; Gate green.

**T15. Client capabilities by controller + DI-driven `Build()`.**
- *Depends on:* T7. *Scope:* `AddRootsCapability(IRootsController)`,
  `AddSamplingCapability(ISamplingController)`, `AddElicitationCapability(IElicitationController)` register
  controllers directly, **dropping the `IRootsCapabilityFactory`/`ISamplingCapabilityFactory`/
  `IElicitationCapabilityFactory.Create()` indirection** (`Client/I*CapabilityFactory.cs`).
  `ClientBuilder.Build()` (dual-mode) resolves `ITransport` (**throw if none** — Build validation parity,
  matching today's `ClientBuilder.cs:90`), `ILoggerFactory`, `ClientInfo`, and the controllers →
  `McpClient`. The `McpClient` ctor takes the three controllers as required-but-nullable params, so
  `Build()` pulls each with **null-tolerant `GetService<T>()`** (decision #3's pattern, client side) — it
  does not type-inject `McpClient`. *Files:* `Client/ClientContextExtensions.cs`, `Client/ClientBuilder.cs`.
  *Acceptance:* client builds via the new API; building with no transport throws at `Build()`; Gate green.

---

### Milestone 4 — Cutover, deletion, docs

**T16. Migrate Server.Tests call sites to the new API.**
- *Depends on:* M2 complete **+ T10b** (most suites build over the in-memory transport and can't migrate
  without the in-memory host). *Scope:* Replace every `new ServerBuilder().WithX()…` / `new
  ClientBuilder().WithX()…` (22 instantiations: 15 `ServerBuilder` + 7 `ClientBuilder`) with `new
  ServerBuilder(name, version)` + `.Context.AddX(...)`. The **13 files**:
  `ConformanceSuite.cs` (the shared `BuildServer`/`ConnectClient` helpers — migrating these two covers most
  suites at once), `Program.cs`, `CancellationTests`, `CompletionTests`, `LoggingTests`, `PaginationTests`,
  `ProgressTests`, `PromptsTests`, `ResourcesTests`, `StdioTests`, `StdioTransportTests`,
  `StreamableHttpTransportTests`, `ToolsTests`. Specifics: in-memory suites' `FixedTransportFactory(...)`
  → register the `InMemoryTransport` via T10b (server) / directly (client); `WithDefaultToolsCapability(
  Json, …)` → `AddToolsCapability(tools => …)` + `AddNewtonsoftJson()`; `Program.cs`'s HTTP path, which
  builds the server **inside** the listener's `onSession` via `.WithStreamableHttpTransport(transport)`, →
  `AddStreamableHttpTransport(baseUrl, path, http => http.ConfigureSession(…))`. **The two raw-handler
  *test methods*** (`StreamableHttpServerToClient`/`StreamableHttpResumability` in
  `StreamableHttpTransportTests`) **keep driving `StreamableHttpListener` directly** (decision #8) — do not
  force them through `ConfigureSession`. *Acceptance:* Gate green entirely on the new API.

**T17. Migrate Client.Tests call sites.**
- *Depends on:* M3 complete. *Scope:* `Client.Tests/Program.cs` (1 `ClientBuilder`) uses
  `WithStdioTransport(json, target, args[1..])` / `WithStreamableHttpTransport(json,
  StreamableHttpClientAdapter(target, …))` and `WithRootsCapability(rootsControllerFactory)` /
  `WithSamplingCapability(samplingControllerFactory)` → `new ClientBuilder(name, version)` +
  `.Context.AddStdioTransport(target, args[1..])` / `.AddStreamableHttpTransport(target)` +
  `.AddRootsCapability(...)` / `.AddSamplingCapability(...)`. Because T15 drops the
  `IXCapabilityFactory` indirection, the stub factories `Client.Tests/RootsControllerFactory.cs` and
  `SamplingControllerFactory.cs` (which implement `IRootsCapabilityFactory`/`ISamplingCapabilityFactory`)
  must be reworked to supply the controllers directly. *Files:* `Client.Tests/Program.cs`,
  `Client.Tests/RootsControllerFactory.cs`, `Client.Tests/SamplingControllerFactory.cs`.
- *Acceptance:* client tests run on the new API.

**T18. Delete the `WithX` service API + dead plumbing + legacy `Build()` branch.**
- *Depends on:* T16, T17. *Scope:* Remove, with no `[Obsolete]` shims:
  - **`ServerBuilder` `WithX`** (`Server/ServerBuilder.cs`): `WithName`, `WithVersion`, `WithTitle`,
    `WithDescription`, `WithLogger`, `WithTransport`, `WithResourcesCapability`, `WithPromptsCapability`,
    `WithToolsCapability`, `WithCompletionCapability`, `WithLoggingCapability`, the parameterless ctor, and
    the dual-mode legacy branch in `Build()`.
  - **`ClientBuilder` `WithX`** (`Client/ClientBuilder.cs`): `WithName`/`WithVersion`/`WithTitle`/
    `WithDescription`/`WithLogger`/`WithTransport`/`WithRootsCapability`/`WithSamplingCapability`/
    `WithElicitationCapability`, the parameterless ctor, and its legacy `Build()` branch.
  - **Transport extension methods + factories:** `WithStdioTransport` (`Server/StdioTransport.cs`,
    `Client/Transports/StdioTransport.cs`), `WithStreamableHttpTransport` + internal `ExistingTransportFactory`
    (`Server/HttpServerTransport.cs`), `WithStreamableHttpTransport` + `StreamableHttpClientTransportFactory`
    (`Client/Transports/StreamableHttpTransport.cs`); `ITransportFactory` (`Shared/ITransportFactory.cs`),
    `StdioTransportFactory` (`Server/StdioTransportFactory.cs`, `Client/Transports/StdioTransport.cs`),
    `FixedTransportFactory` (`Server.Tests/`, replaced by T10b's in-memory host).
  - **Tools/logger extensions:** `WithDefaultToolsCapability` (`Server/DefaultToolsController.cs`),
    `WithConsoleLogger` (`Adapter.ConsoleLogger/ServerBuilderExtensions.cs`).
  - **Client capability factories:** `IRootsCapabilityFactory`/`ISamplingCapabilityFactory`/
    `IElicitationCapabilityFactory` (`Client/I*CapabilityFactory.cs`).
  - *Acceptance:* no `WithX` service methods remain; the builders only construct via `(name, version)`;
    Gate green.

**T19. Docs.**
- *Depends on:* T18. *Scope:* Update `README.md` examples to the new API; document deviations from
  `API_DESIGN.md` (`IServer.Log` on HTTP host, decision #6); optionally mark `API_DESIGN.md` implemented.
- *Files:* `README.md`, `API_DESIGN.md`. *Acceptance:* docs match the shipped API.

---

## 4. Execution order & parallelism

1. **T1** unblocks all.
2. After T1, in parallel: **T2**, **T3 → T4** (sequential, same internals), **T5**, **T6**, **T7**.
3. **T8** after T7.
4. **T9** after T8 → **T10 → T10b**; **T11** (after T3+T5+T7), **T12** (after T7) in parallel.
5. **T13a** after **T4**+T9+T10 → **T13b** after T11+T13a (T4 already pulled in by T13a).
6. **T14**/**T15** (client) after T6/T7 — overlap M2.
7. **T16** after M2 **and T10b**, **T17** after M3, then **T18** (delete), then **T19** (docs).

## 5. Risks / watch-items
- **Gate-green through cutover** rests entirely on the dual-mode `Build()` (decision #5); verify legacy
  tests still pass after T7 before proceeding.
- **Tools aggregation** (decision #2) is the subtlest piece — the 2-session HTTP test (T13b) is the
  proof: shared + per-session tools both visible, siblings isolated, zero-tools server silent.
- **In-memory host is on the critical path** (decision #10 / T10b): ~12 of 18 suites build over
  `FixedTransportFactory(InMemoryTransport)`, so T16 cannot migrate them until a server can be built over a
  pre-built `ITransport`. Sequence T10b before T16.
- **Per-connection transport must live in a scope, not the root.** The HTTP host gets a *different*
  `HttpServerTransport` per connection, so the T9 factory can only resolve it from a per-connection child
  scope — which is why T13a (not just T13b) depends on T4. A "resolve from root" shortcut cannot wire the
  per-connection transport.
- **Session-server activation** must be factory-based (decision #3); a typed registration will throw
  (optional controllers + `bool` ctor param).
- **Build()-time validation** of serializer relies on the host ctor resolving `IJson` + eager singleton
  realization; confirm the failure surfaces at `Build()`, not `Start()`.
- **Child container thread-safety**: children must eagerly realize their own singletons (read-only after
  construction) since HTTP creates/resolves them concurrently.
- **Core must not depend on HTTP adapters** — both `AddStreamableHttpTransport` overloads live in adapter
  assemblies; no `HttpServerTransport` relocation needed (decision #8).
- **`Build()` sync, `Start()/Connect()` async** — keep all I/O out of `Build()`.
