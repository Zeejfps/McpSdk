# MCP SDK — Target Public API

The intended construction API for the DI/`IContext` migration. **Part 1** is how it's used;
**Part 2** is the surface that backs it. This is a design target, not yet implemented.

## Design rules this encodes

- **Required** (name, version, transport args) → factory params. Can't forget them.
- **Optional** (title, description) → `ConfigureInfo(...)`. Grouped, discoverable.
- **Everything else** → `Context.AddX(...)`. One mental model: learn `AddTools`, guess the rest.
- **Tools** use a fluent `IToolsBuilder` — instance *or* container-activated (`<T>`) tools.
- `Build()` = sync wiring (fails fast). `Start()`/`Connect()` = async I/O.

---

## Part 1 — Usage

### Server over stdio

```csharp
var serverBuilder = StdioMcpServer.CreateBuilder("Demo Server", "1.0.0");

serverBuilder.Context
    .AddNewtonsoftJson()
    .AddConsoleLogger()
    .ConfigureInfo(info =>                       // optional metadata lives here, NOT in CreateBuilder
    {
        info.Title       = "Demo";
        info.Description = "A demo MCP server";
    })
    .AddToolsCapability(tools => tools
        .AddTool(new TestTool())                 // bring your own instance
        .AddTool<WeatherTool>()                  // container-activated: ctor deps injected
        .WithPageSize(50));                      // tools/list pagination (omit = single page)

var mcpServer = serverBuilder.Build();           // sync: all wiring validated here
await mcpServer.Start();
```

### Server over Streamable HTTP

Base URL and path are **separate** — no `/mcp` duplication. `baseUrl` = host:port to bind,
`path` = route.

An HTTP server is 1 listener = many sessions = one `McpServer` **per session**. Globals go on the
root `Context` (Singletons, shared by all sessions); per-session services go in `ConfigureSession`,
where `session.Context` is *that session's own* registration surface and `session` also carries its
identity. See [§ Streamable HTTP server — per-session model](#streamable-http-server--per-session-model-configuresession)
for the full rationale.

```csharp
var httpServerBuilder = StreamableHttpMcpServer.CreateBuilder(
    "Demo Server", "1.0.0",
    "http://localhost:3000",                     // baseUrl (host:port to bind, NOT ".../mcp")
    "/mcp");                                     // path

// GLOBAL — Singletons on the root context, shared by every session
httpServerBuilder.Context
    .AddNewtonsoftJson()
    .AddConsoleLogger()
    .ConfigureInfo(info => info.Title = "Demo")
    .AddToolsCapability(tools => tools.AddTool(new EchoTool()));   // shared, stateless tool

// PER SESSION — runs for each new session; session.Context is that session's own registrations
httpServerBuilder.ConfigureSession(session =>
{
    session.Context.AddToolsCapability(tools => tools.AddTool(new CartTool()));   // per-session state
    if (session.Origin == "https://admin.example.com")                            // auth-gated visibility
        session.Context.AddToolsCapability(tools => tools.AddTool(new AdminTool()));
});

var httpServer = httpServerBuilder.Build();      // the listener, surfaced as an IServer
await httpServer.Start();                         // accepts connections; builds one McpServer per session
```

### Client over stdio

```csharp
var clientBuilder = StdioMcpClient.CreateBuilder(
    "Echo client", "1.0.0",
    "echo");                                     // command (+ params string[] args)

clientBuilder.Context
    .AddNewtonsoftJson()
    .AddConsoleLogger()
    .AddSamplingCapability(new MySamplingController())
    .AddRootsCapability(new MyRootsController());

var client = clientBuilder.Build();
await client.Connect();
```

### Client over Streamable HTTP

Client takes the single **full** endpoint url (it POSTs to it) — this one keeps the path.

```csharp
var httpClientBuilder = StreamableHttpMcpClient.CreateBuilder(
    "Echo client", "1.0.0",
    "http://localhost:3000/mcp");

httpClientBuilder.Context
    .AddNewtonsoftJson()
    .AddConsoleLogger();

var httpClient = httpClientBuilder.Build();
await httpClient.Connect();
```

---

## Part 2 — API Surface

### Entry points

One per transport; each demands exactly its required args.
`StdioMcp*` live in core (`McpSdk.Server` / `McpSdk.Client`); `StreamableHttp*` live in their
adapter assemblies, so core stays transport-dependency-free.

```csharp
public static class StdioMcpServer
{
    public static ServerBuilder CreateBuilder(string name, string version);
}

public static class StreamableHttpMcpServer        // McpSdk.Adapter.StreamableHttpServer
{
    // NOTE: returns a StreamableHttpServerBuilder (not ServerBuilder) — it owns ConfigureSession.
    public static StreamableHttpServerBuilder CreateBuilder(string name, string version, string baseUrl, string path);
}

public static class StdioMcpClient
{
    public static ClientBuilder CreateBuilder(string name, string version, string command, params string[] args);
}

public static class StreamableHttpMcpClient        // McpSdk.Adapter.StreamableHttpClient
{
    public static ClientBuilder CreateBuilder(string name, string version, string endpointUrl);
}
```

### Builders

Thin; all real config flows through `Context`.

```csharp
public sealed class ServerBuilder
{
    public IContext Context { get; }               // the only knob; transport is pre-registered by the factory
    public IServer Build();                         // sync, validates wiring (missing IJson etc. throws here)
}

public sealed class ClientBuilder
{
    public IContext Context { get; }
    public IClient Build();
}
```

The Streamable HTTP server gets its **own** builder — `ConfigureSession` lives only here, because
only this transport has a per-session lifecycle.

```csharp
public sealed class StreamableHttpServerBuilder
{
    public IContext Context { get; }                       // root — Singletons, shared by all sessions

    /// Runs for each new session. session.Context is that session's own registration surface;
    /// anything added there lives for the session and is disposed when it ends.
    public StreamableHttpServerBuilder ConfigureSession(Action<ISession> configure);

    public IServer Build();                                 // the listener, surfaced as an IServer
}

/// A live MCP session: its own DI scope plus the connection's identity.
public interface ISession
{
    IContext Context { get; }       // per-session registrations (resolve against root for Singletons)
    string SessionId { get; }       // the Mcp-Session-Id issued on initialize
    string Origin { get; }          // request Origin (null for non-browser clients) — for auth gating
    ITransport Transport { get; }   // this session's HttpServerTransport (auto-wired into Context)
}
```

### `IContext` — shared registrations (both sides)

```csharp
public static class SharedContextExtensions
{
    public static IContext AddNewtonsoftJson(this IContext c);     // McpSdk.Adapter.Newtonsoft.Json
    public static IContext AddSystemTextJson(this IContext c);     // McpSdk.Adapter.System.Text.Json
    public static IContext AddConsoleLogger(this IContext c);      // McpSdk.Adapter.ConsoleLogger
    public static IContext AddLogger(this IContext c, ILoggerFactory loggerFactory);
}
```

### `IContext` — server side

Only in scope with `using McpSdk.Server`, so it can't leak to clients. Every capability is the
**same shape** — `Add<Name>Capability(...)`.

```csharp
public static class ServerContextExtensions
{
    public static IContext ConfigureInfo(this IContext c, Action<ServerInfoOptions> configure);

    public static IContext AddToolsCapability(this IContext c, Action<IToolsBuilder> configure);
    public static IContext AddToolsCapability(this IContext c, IToolsController controller);   // BYO controller

    public static IContext AddPromptsCapability(this IContext c, IPromptController controller);
    public static IContext AddResourcesCapability(this IContext c, IResourcesController controller);
    public static IContext AddCompletionCapability(this IContext c, ICompletionController controller);
    public static IContext AddLoggingCapability(this IContext c);   // marker: advertises `logging`
}
```

> Prompts/Resources can grow an `Action<IPromptsBuilder>` lambda form later, mirroring tools, once
> they have a `Default*Controller`. Keeping the BYO form now keeps the surface honest.

### `IContext` — client side

Only in scope with `using McpSdk.Client`.

```csharp
public static class ClientContextExtensions
{
    public static IContext ConfigureInfo(this IContext c, Action<ClientInfoOptions> configure);

    public static IContext AddRootsCapability(this IContext c, IRootsController controller);
    public static IContext AddSamplingCapability(this IContext c, ISamplingController controller);
    public static IContext AddElicitationCapability(this IContext c, IElicitationController controller);
}
```

### Tools builder

```csharp
public interface IToolsBuilder
{
    /// Register a tool you constructed yourself. Lives as a process-wide singleton.
    IToolsBuilder AddTool(IToolHandler handler);

    /// Register a tool the container activates — its constructor deps (IJson, your services)
    /// are injected. Use this when a tool has dependencies, or when you want a fresh instance
    /// per session under the HTTP per-session lifetime.
    IToolsBuilder AddTool<THandler>() where THandler : class, IToolHandler;

    /// tools/list page size. Omit -> a single page with no cursor.
    IToolsBuilder WithPageSize(int pageSize);
}
```

### Options shapes

No `Name`/`Version` here — those are required `CreateBuilder` params.

```csharp
public sealed class ServerInfoOptions
{
    public string Title { get; set; }
    public string Description { get; set; }
}

public sealed class ClientInfoOptions
{
    public string Title { get; set; }
    public string Description { get; set; }
}
```

---

## Streamable HTTP server — per-session model (`ConfigureSession`)

Streamable HTTP is 1 listener = many sessions, one `McpServer` per session. We make that split
**two registration surfaces** on `StreamableHttpServerBuilder`:

- **`Context`** — the root. Registered once, shared by every session (json, logger, stateless tools).
- **`ConfigureSession(session => …)`** — runs for each new session. `session.Context` is *that
  session's own* registration surface; what you add there lives for the session and is disposed when
  it ends. `session` also carries the connection's identity (`SessionId`, `Origin`, `Transport`).

This *dissolves* the old "should tools be global or per-session?" question — it's no longer one
policy for the whole server. Shared tool? Register it on `Context`. Per-session tool/state? Register
it inside `ConfigureSession`. Same mechanism for any service. **Default to `Context` (global):** a
tool's *definition* is the server's advertised capability — identical for every session — and a
well-written handler is stateless (args in, result out), so one shared thread-safe instance is
simplest and cheapest, exactly like ASP.NET endpoints. Reach for `ConfigureSession` only when a
handler genuinely needs per-connection state or identity (e.g. auth-gated tool visibility).

### What this maps onto (grounded in `StreamableHttpListener`)

`IJson` and `ILoggerFactory` are *already* shared today (ctor args reused for every transport); only
`HttpServerTransport` and the `McpServer` are per-session. The model just makes that configurable:

| Service | Where you register it | Lifetime |
|---|---|---|
| `IJson`, `ILoggerFactory` | `Context` | shared by all sessions |
| stateless tools (the usual case) | `Context` | shared by all sessions |
| `HttpServerTransport` | (auto-wired into `session.Context`) | per session |
| `McpServer` | (built from `session.Context`) | per session |
| per-session tools / state | `ConfigureSession` | per session |

### The shape

```csharp
var http = StreamableHttpMcpServer.CreateBuilder(
    "Demo Server", "1.0.0", "http://localhost:3000", "/mcp");

// GLOBAL — registered once on the root context, shared by every session
http.Context
    .AddNewtonsoftJson()
    .AddConsoleLogger()
    .AddToolsCapability(tools => tools.AddTool(new EchoTool()));   // shared, stateless tool

// PER SESSION — runs for each new session; session.Context is that session's own registrations,
// session carries its identity, so per-session tool visibility is a plain `if`.
http.ConfigureSession(session =>
{
    session.Context.AddToolsCapability(tools => tools.AddTool(new CartTool()));   // per-session state
    if (session.Origin == "https://admin.example.com")
        session.Context.AddToolsCapability(tools => tools.AddTool(new AdminTool()));
});

var server = http.Build();
await server.Start();
```

- ➕ Per-session is **visible** — a lambda that clearly runs per session — without exposing a lifetime enum.
- ➕ One rule to learn: *global → `Context`, per-session → `ConfigureSession`.*
- ➕ `session.Context` reuses the whole `AddXxxCapability` surface; nothing new to learn there.
- ➕ `session` exposes identity (`Origin` / `SessionId`) → clean auth-gated tools.
- `ConfigureSession` exists **only** on `StreamableHttpServerBuilder` — stdio has no sessions, so it
  never sees this surface.

### Backing implementation

`StreamableHttpMcpServer` wraps `StreamableHttpListener` and owns the **root provider** built from
`Context` (Singletons: json, logger, shared tools). For each new session, in the listener's
`onSession` callback it:

1. creates the session's child container/scope (inheriting the root's singletons),
2. seeds the session's `HttpServerTransport` into `session.Context`,
3. runs the `ConfigureSession` callback to add that session's registrations,
4. resolves + starts the per-session `McpServer` from `session.Context`,
5. disposes the scope (and its per-session services) when the session ends (`DELETE` / drop).

> **One required change to `DiContainer`:** it must support a **child/scoped container** — a session
> container that resolves its own registrations but falls back to the root for Singletons, and
> disposes its own instances on close. Today it's a single flat Singleton/Transient container.
> Standard, well-trodden DI machinery, but it's the prerequisite that makes `ConfigureSession` work.
