# Verification Plan — `IContext` DI Builder Migration

Companion to [`IMPLEMENTATION_PLAN.md`](./IMPLEMENTATION_PLAN.md). For every task **T1–T19** this says
*how to prove it is actually done* — the exact command to run, what to look for, and the pass bar.

The implementation plan's per-task **Acceptance** lines say *what* must be true. This plan makes each one
**checkable**: it pins the regression baseline, turns the "throws at `Build()`" acceptances into machine-
checked assertions (no existing suite covers them), and adds structural greps for the cutover/deletion
tasks where "green gate" alone can't prove the old API is gone.

---

## 0. Baseline & the universal gate

Captured on branch `improved_di`, **before any task lands**:

| Signal | Baseline |
|---|---|
| `dotnet build MCPSharp.sln` | `0 Error(s)` (5 pre-existing warnings) |
| `dotnet run --project Server.Tests -- conformance` | **`=== 331 passed, 0 failed ===`**, exit code `0` |
| Conformance suites (array in `ConformanceTests.RunAll`) | **18** |
| Assertion call sites in `Server.Tests` | 239 `Assert(` + 88 `AssertEqual(` |

**The gate.** `dotnet run --project Server.Tests -- conformance` prints `=== <P> passed, <F> failed ===`
and **exits with `F`** (the failure count). This is the single source of truth.

**Two invariants that must hold after _every_ task:**

1. **`F == 0`.** The exit code is `0`. No assertion ever fails.
2. **`P` is monotonically non-decreasing.** It starts at 331 and only goes up (T2–T5 add the DI suite;
   T11/T13b add capability/aggregation assertions). **A drop in `P` is a silent regression** — it almost
   always means a suite stopped being constructed in `RunAll`, or a test body threw *before* reaching its
   asserts (the harness counts a thrown body as a single `FAIL`, so watch for `FAIL: threw …`). Treat any
   unexplained decrease as a failure even if `F == 0`.

**Capture a reference log once, diff against it after each task** (PASS/FAIL lines are stable and ordered):

```sh
SP=/private/tmp/claude-501/-Users-zee-seriesai-src-EnvMcp/3821880b-ee8c-4f14-9f07-45870b9eb048/scratchpad
dotnet run --project Server.Tests -- conformance | tee "$SP/baseline.txt"
# after a task:
dotnet run --project Server.Tests -- conformance | tee "$SP/after.txt"
grep -E 'PASS|FAIL' "$SP/baseline.txt" > "$SP/b.lines"
grep -E 'PASS|FAIL' "$SP/after.txt"    > "$SP/a.lines"
diff "$SP/b.lines" "$SP/a.lines"   # expect only ADDED lines, never a PASS→FAIL or a removed PASS
```

---

## 1. The six verification techniques

Each task below is tagged with the techniques it needs.

- **(A) Gate-green** — build + conformance stay at `F == 0`, `P` non-decreasing. *Every task.*
- **(B) New assertions** — the task adds named `Assert(...)`/`AssertEqual(...)` lines (usually in the DI
  suite, or a capability suite) that appear as new `PASS:` lines. Verify by name in the run log.
- **(C) Negative test via `Throws<T>`** — the implementation plan's "throws at `Build()`" / "throws on
  missing dep" acceptances are **not** covered by any existing suite. Encode each as an assertion using
  the `Throws<TException>(Action, name)` helper ported in **T2**. A "throws" acceptance is only *verified*
  once it is a `PASS:` line — never leave it as eyeballed behavior.
- **(D) Structural grep** — for cutover/deletion tasks, prove a symbol is **gone** (or **only** the new
  form remains). A green gate cannot prove absence; grep can.
- **(E) Behavioral / wire inspection** — drive a real client↔server pair and assert on observable
  protocol output (`FindInitializeResult`, advertised capabilities, the 2-session HTTP aggregation test).
- **(F) Manual review checkpoint** — a reviewer must eyeball something automation can't reach (e.g. "no
  I/O happens in `Build()`", correct dependency direction). Listed explicitly so it isn't skipped.

> **Where the new assertions live.** DI-container facts (T3–T5) go in the `DiContainerTests` suite from
> T2. Capability/behavior facts attach to the suite that owns the feature (`ToolsTests`, `LoggingTests`,
> the new HTTP aggregation test, …). Pure-resolution facts with no natural feature home (T6/T9/T14) can
> ride in `DiContainerTests` or a tiny dedicated `RegistrationTests` suite — either way they must show up
> as named `PASS:` lines in the gate.

---

## 2. Per-task verification

### Milestone 0 — Container foundation

**T1 — Port DI container core + options/logging into `Shared`.** *(A, F)*
- Run: `dotnet build MCPSharp.sln` → `0 Error(s)`; gate still **331/0** (nothing consumes the new code).
- Check: the nine files exist under `Shared/` in namespace `McpSdk.Shared`:
  `ls Shared/IContext.cs Shared/DiContainer.cs Shared/ServiceProvider.cs Shared/ServiceDescriptor.cs Shared/ContextRegistrationExtensions.cs Shared/ServiceProviderExtensions.cs Shared/IOptions.cs Shared/OptionsContextExtensions.cs Shared/LoggingContextExtensions.cs`
- (F) Confirm nothing outside `Shared` references the new types yet:
  `grep -rln --include='*.cs' "DiContainer\|IContext" Server Client Adapter.* | grep -v Shared/` → empty.
- **Pass:** compiles; gate unchanged at 331/0; files present; no premature consumers.

**T2 — Port the container test set as the 19th suite.** *(A, B)*
- Run the gate. **Suite count 18 → 19**; a new section header prints (e.g. `=== DI Container ===`).
- Check the 11 ported bodies each appear as named tests, including `OptionsConfigureAndResolve` (proves
  the T1 `Configure<T>`/`IOptions<T>` infra is wired). Confirm `Throws<T>` helper exists for later tasks.
  `dotnet run --project Server.Tests -- conformance | grep -A1 -iE 'circular|ambiguous|last.*win|options'`
- **Pass:** `=== <P> passed, 0 failed ===` with **P > 331**; the DI section and its 11 tests are in the log.
- **This is the harness for C-type checks below** — if `Throws<T>` isn't in place, T3/T5/T10/T11/T15
  cannot be machine-verified.

**T3 — Multi-registration + `GetServices<T>()` + per-descriptor singleton cache.** *(A, B)*
- New DI assertions (must appear as `PASS:`):
  - register two `IFoo` → `GetService<IFoo>()` returns the **last**; `GetServices<IFoo>()` returns
    **both, in registration order** (assert count == 2 *and* order).
  - two distinct singleton **descriptors** of the same service realize **two** instances (per-descriptor
    cache), while one descriptor resolved twice returns the **same** reference.
- **Pass:** those named assertions PASS; gate `F == 0`; P up.

**T4 — Child / scoped container.** *(A, B)*
- New DI assertions: child override wins over parent; a parent **singleton is shared by reference** into
  the child (assert `ReferenceEquals`); sibling children are isolated; concurrent child creation is safe
  (spin up N children on N threads, assert no throw and each resolves independently).
- (F) Review that a child **eagerly realizes its own singletons** (read-only after construction) — the
  thread-safety claim rides on this.
- **Pass:** child-scope assertions PASS; gate `F == 0`.

**T5 — `ActivatorUtilities.CreateInstance(provider, type)`.** *(A, B, C)*
- New DI assertions: activate an **unregistered** concrete type whose ctor takes a registered dependency
  **and** an `IServiceProvider` → succeeds. **(C)** activating a type with a missing dependency throws
  (`Throws<…>`); an equal-arity ambiguous ctor throws — matching the existing selection rule.
- (F) Confirm selection logic was **extracted and shared** with `ServiceProvider.SelectConstructor`
  (not duplicated), so registered- and unregistered-type activation can't diverge.
- **Pass:** activation + throw assertions PASS; gate `F == 0`.

### Milestone 1 — Shared registrations + builder skeleton

**T6 — Serializer & logger context extensions.** *(A, B, F)*
- New assertions over a built provider:
  - `AddNewtonsoftJson()` → `GetService<IJson>()` is `NewtonsoftJson`; `AddSystemTextJson()` → `SystemJson`.
  - `AddLogger(factory)` → `GetService<ILoggerFactory>()` returns that factory.
  - **The `AddConsoleLogger` asymmetry (plan T6) is resolved:** assert the *server* registration resolves
    `ServerConsoleLoggerFactory` and the *client* registration resolves `ClientConsoleLoggerFactory`
    (whichever disambiguation was chosen — two overloads or a marker). This is the one spot the plan flags
    as undecided, so it gets its **own** explicit assertion.
- **Pass:** resolution assertions PASS; additive only (not yet in `Build()`); gate `F == 0`.

**T7 — Builder ctors + `Context` + dual-mode `Build()` skeleton.** *(A, B, F)*
- **Regression-critical:** the entire legacy gate must still be **331/0** (decision #5 hinges on this —
  verify before moving on).
- New assertions: `new ServerBuilder("n","1.0").Context` is non-null and usable; ctor seeds name/version
  into `ServerInfoOptions`/`ClientInfoOptions` (resolve and check) and a default `NullLoggerFactory`.
- (F) Review `Build()` is genuinely **dual-mode**: resolves `IServerHost`/transport *if registered*, else
  the legacy `WithX` field path; parameterless ctor + `WithName/WithVersion` still work.
- **Pass:** legacy gate unchanged at 331/0; new ctor/Context assertions PASS.

**T8 — `ConfigureInfo` + options shapes.** *(A, B, E)*
- (E) Behavioral: build a server with `ConfigureInfo(i => { i.Title=…; i.Description=…; })`, connect a
  client, and assert title/description surface in the advertised `initialize` result
  (`FindInitializeResult(serverEnd.Sent)` → check the `serverInfo`/title field). Or, minimally, resolve
  `ServerInfo`/`ClientInfo` and assert the fields. Cover both server and client.
- **Pass:** title/description flow through; gate `F == 0`.

### Milestone 2 — Server pipeline

**T9 — Factory registration for the session `McpServer`.** *(A, B)*
- New assertion: a provider with **only** transport + serializer + tools registered resolves a non-null
  `McpServer` (the factory pulls optional controllers with null-tolerant `GetService<T>()` and reads
  `loggingEnabled`/`ServerInfo` from options — no type-injection throw).
- **Pass:** `McpServer` resolves from a minimal provider; gate `F == 0`.

**T10 — `IServerHost` + stdio host + `AddStdioTransport()` (server).** *(A, C, E)*
- (E) Stdio round-trip on the **new API**: point the `stdio-server` entry (or a scratch harness) at
  `new ServerBuilder("…","…").Context.AddNewtonsoftJson()…AddStdioTransport()…` and run `StdioTransportTests`
  (it spawns the child process and speaks MCP over real pipes). Suite stays green.
- (C) **Missing-serializer-throws-at-`Build()`:** assert `Throws<…>(() => new ServerBuilder("n","1").
  Context.AddStdioTransport(); builder.Build(), "missing serializer throws at Build")` — relies on the
  singleton `ITransport` being **eagerly realized inside `Build()`**.
- (C) **No-transport-throws-at-`Build()`:** a builder with a serializer but no host throws a clear "no
  transport registered" error at `Build()` (not an NRE later).
- **Pass:** `StdioTransportTests` green via new API; both throw-assertions PASS; gate `F == 0`.

**T10b — In-memory server host (rewire `BuildServer`/`ConnectClient`).** *(A)*
- This is the **highest-leverage automatic check in the whole plan.** Rewiring the shared
  `ConformanceSuite.BuildServer(InMemoryTransport)` / `ConnectClient` helpers to the new API routes **~12
  of the 18 suites** through the new server-build path at once.
- Run the gate. **Pass:** still **331/0** (or higher) with `BuildServer` on the new API. If any in-memory
  suite drops, the in-memory host is wrong. **Gate this before T16** — T16 can't migrate the remaining
  sites until this is green.

**T11 — Tools: `IToolsBuilder` + composite + controller overload.** *(A, B, C, E)*
- `ToolsTests` + `PaginationTests` stay green via new API.
- (B) `AddTool<THandler>()` whose ctor takes a registered dependency → tool is listed/callable (activation
  via `ActivatorUtilities` at session scope).
- (E) **Zero-tools server does not advertise tools:** build a server with no tools, inspect the advertised
  `initialize` capabilities, assert `tools` is absent (preserves today's null-controller suppression).
- (C) An `AddTool<T>()` with a missing dependency or ambiguous ctor fails the same way as T5.
- **Pass:** tools/pagination green; the three new assertions PASS; gate `F == 0`.

**T12 — Remaining server capabilities as `Add<Name>Capability`.** *(A)*
- `PromptsTests`, `ResourcesTests`, `CompletionTests`, `LoggingTests` all pass via the new API (these four
  suites *are* the verification once their `BuildServer` path uses `Add…Capability`).
- (E) Logging: assert the advertised capabilities include `logging` only when `AddLoggingCapability()` was
  called (the `loggingEnabled` flag read by T9's factory actually flips the advertisement).
- **Pass:** four suites green; logging advertisement gated correctly; gate `F == 0`.

**T13a — Streamable HTTP host skeleton (per-connection scope, no `ConfigureSession`).** *(A, F)*
- `StreamableHttpTransportTests` passes via the new API, **including** the two raw-handler test methods
  (`StreamableHttpServerToClient`, `StreamableHttpResumability`) which must still drive
  `StreamableHttpListener` directly (decision #8 — they do **not** go through `ConfigureSession`).
- (F) Review: per-connection `HttpServerTransport` is registered into a **child scope** (not the root),
  and `McpServer` is resolved from that child. Confirm core does not gain a dependency on the HTTP adapter.
- **Pass:** HTTP suite green (incl. raw methods); gate `F == 0`.

**T13b — Per-session child scope + `ConfigureSession` + aggregation.** *(A, B, E)*
- **The marquee behavioral test — must be newly added** (decision #2's proof, plan risk-list item):
  two concurrent sessions over the HTTP host where the root registers a shared `EchoTool` and
  `ConfigureSession` adds `CartTool` to every session and `AdminTool` only for the admin origin. Assert:
  1. session A `tools/list` = `EchoTool` + `CartTool` (+ `AdminTool` iff admin origin),
  2. session B sees the shared tool but **not** session A's per-session additions (sibling isolation),
  3. neither session is missing the shared `EchoTool` (overlay = root ∪ session, child wins on conflict).
- **Pass:** the 2-session aggregation/isolation assertions PASS; gate `F == 0`, P increased.

### Milestone 3 — Client pipeline

**T14 — Client transports as registrations.** *(A, B)*
- (B) `AddStdioTransport(command, args)` and `AddStreamableHttpTransport(endpointUrl)` each resolve a
  non-null `ITransport` from the container (HTTP builds the `StreamableHttpClientAdapter` **internally**
  from the bare URL + DI `ILoggerFactory` — assert the registration takes a *string*, not a pre-built
  adapter).
- **Pass:** client transports resolve; gate `F == 0`.

**T15 — Client capabilities by controller + DI-driven `Build()`.** *(A, C)*
- The client-capability suites — `RootsTests`, `SamplingTests`, `ElicitationTests` — stay green once
  `ConnectClientWith` registers controllers via `AddRootsCapability(IRootsController)` etc. (the
  `IXCapabilityFactory.Create()` indirection is dropped). These suites *are* the behavioral check.
- (C) **No-transport-throws-at-`Build()`** on the client (parity with today's `ClientBuilder.cs:90`):
  `Throws<…>(() => new ClientBuilder("n","1").Build(), "client Build throws with no transport")`.
- **Pass:** three client suites green via controller API; no-transport throw asserts; gate `F == 0`.

### Milestone 4 — Cutover, deletion, docs

**T16 — Migrate Server.Tests call sites.** *(A, D)*
- (A) Gate **entirely on the new API** at `F == 0`, P ≥ 331.
- (D) **Prove the old API is gone from the test project:**
  `grep -rnE '\.With(Name|Version|Title|Description|Logger|Transport|.*Capability)\b' Server.Tests --include='*.cs'`
  → **empty**; `grep -rn "FixedTransportFactory\|new ServerBuilder()\|new ClientBuilder()" Server.Tests`
  → empty (no parameterless ctors, no `FixedTransportFactory`).
- (E) HTTP demo path: `Program.cs`'s `streamable-http-server` mode now uses
  `AddStreamableHttpTransport(baseUrl, path, http => http.ConfigureSession(…))`; the two raw-handler test
  methods are untouched.
- **Pass:** gate green on new API; both greps empty.

**T17 — Migrate Client.Tests call sites.** *(A, D, E)*
- `Client.Tests` is a **manual demo, not in the conformance gate.** Verify by running it end-to-end:
  1. start a server: `dotnet run --project Server.Tests -- streamable-http-server http://localhost:3000`
  2. drive it: `dotnet run --project Client.Tests -- http://localhost:3000/mcp` → lists tools, calls
     `get-forecast`, prints content with no error.
  3. repeat over stdio: `dotnet run --project Client.Tests -- dotnet <…>/McpSdk.Server.Tests.dll stdio-server`.
- (D) `grep -rnE '\.With' Client.Tests --include='*.cs'` → empty; the two stub factories
  (`RootsControllerFactory`, `SamplingControllerFactory`) now implement the **controller** interfaces
  directly (no `IXCapabilityFactory`).
- **Pass:** both demo runs complete; greps empty; `dotnet build` clean.

**T18 — Delete the `WithX` service API + dead plumbing + legacy `Build()`.** *(A, D, F)*
- (A) `dotnet build MCPSharp.sln` → 0 errors; gate green on the new API.
- (D) **Absence greps across the whole repo** (each must be empty):
  - `grep -rnE 'public .*\bWith(Name|Version|Title|Description|Logger|Transport|Roots|Sampling|Elicitation|Tools|Prompts|Resources|Completion|Logging)Capability?\b' --include='*.cs' .`
  - `grep -rn "ITransportFactory\|StdioTransportFactory\|ExistingTransportFactory\|FixedTransportFactory\|StreamableHttpClientTransportFactory" --include='*.cs' .`
  - `grep -rn "IRootsCapabilityFactory\|ISamplingCapabilityFactory\|IElicitationCapabilityFactory" --include='*.cs' .`
  - `grep -rn "WithDefaultToolsCapability\|WithConsoleLogger" --include='*.cs' .`
  - `grep -rn "CreateBuilder" --include='*.cs' .` (confirms no static factory crept in)
  - parameterless builder ctors gone: `grep -rn "new ServerBuilder()\|new ClientBuilder()" --include='*.cs' .` → empty.
  - `grep -rin "obsolete" Server Client Adapter.* --include='*.cs'` → empty (decision: no shims).
- (F) Review `ServerBuilder.Build()`/`ClientBuilder.Build()` have **no** legacy branch left — single DI path.
- **Pass:** all absence greps empty; build + gate green.

**T19 — Docs.** *(A, D, F)*
- (D) `grep -nE '\.With|FixedTransportFactory|CreateBuilder' README.md` → empty (examples use the new API).
- (F) Review: `README.md` examples match the shipped API; the `IServer.Log`-on-HTTP-host deviation
  (decision #6) and any other `API_DESIGN.md` deviations are documented; `API_DESIGN.md` optionally marked
  implemented.
- (F) Best-effort: paste each README server/client snippet into a scratch program and confirm it compiles
  against the built assemblies (docs drift is invisible to the gate).
- **Pass:** README is new-API only; deviations documented; snippets compile.

---

## 3. Milestone exit gates

Run before declaring a milestone done:

- **M0 (T1–T5):** gate green; DI suite present with multi-registration, child-scope, and
  `ActivatorUtilities` (incl. throw) assertions all PASS; P meaningfully above 331.
- **M1 (T6–T8):** legacy gate still 331/0 (dual-mode intact); `IJson`/`ILoggerFactory`/`ServerInfo`
  resolution + `ConfigureInfo` assertions PASS.
- **M2 (T9–T13b):** stdio, in-memory, and HTTP server paths all run on the new API; missing-serializer and
  no-transport throws are PASS lines; the 2-session aggregation test passes. Gate green.
- **M3 (T14–T15):** client transports + capabilities on the new API; client no-transport throw is a PASS
  line; Roots/Sampling/Elicitation suites green.
- **M4 (T16–T19):** every absence grep empty; build + gate green entirely on the new API; demos run; docs
  match.

## 4. Final acceptance (post-T19)

1. `dotnet build MCPSharp.sln` → `0 Error(s)`.
2. `dotnet run --project Server.Tests -- conformance` → `=== <P> passed, 0 failed ===`, exit `0`,
   with **P ≥ 331** and the DI suite included (≥ 19 suites).
3. The whole-repo absence greps from **T18** are all empty — the `WithX`/factory API is gone with no shims.
4. Both `Client.Tests` demo runs (stdio + HTTP) complete end-to-end.
5. `README.md` shows only the new `(name, version)` + `Context.AddX(...)` API.

If all five hold, the migration in `IMPLEMENTATION_PLAN.md` is verifiably complete.
