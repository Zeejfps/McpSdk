# MCP SDK — Target Public API

This describes the **desired** construction API for the SDK: how an application builds an MCP server
or client. It is a design goal, not yet implemented, and it documents *what* the API should look
like — not how it will be built internally.

The SDK uses a small dependency-injection container behind an `IContext` registration surface
(modeled on `Microsoft.Extensions.DependencyInjection`). Every builder exposes that `Context`, and
adapters contribute features as `Context.AddX(...)` extension methods. **The transport is registered
the same way** — `Context.AddStdioTransport()` or `Context.AddStreamableHttpTransport(...)` — so
there is a single builder type per role (server / client) — `new ServerBuilder(name, version)` /
`new ClientBuilder(name, version)` — and the transport is just another registration rather than a
separate factory or builder type.

## Design rules

- **Name and version** are **required constructor parameters** of the builder
  (`new ServerBuilder(name, version)` / `new ClientBuilder(name, version)`) — you can't forget them, the
  code won't compile without them. There is no static `CreateBuilder` factory; you construct the builder
  directly.
- **The transport** is a required registration: `Context.AddStdioTransport()` /
  `Context.AddStreamableHttpTransport(...)`. Its address (baseUrl + path, launch command, or
  endpoint url) is a required parameter of *that* method. Forgetting to register a transport at all
  is caught by `Build()`, not by the compiler.
- **Optional metadata** (title, description) goes through `ConfigureInfo(...)`.
- **Everything else** — serializer, logger, capabilities — is registered on `Context.AddX(...)`.
  Every capability follows the same `Add<Name>Capability(...)` shape, so learning one teaches the rest.
- `Build()` is synchronous and validates the wiring (a missing serializer *or* transport throws
  here, not later). `Start()` / `Connect()` are asynchronous (they do I/O).

---

## Part 1 — Usage

### Server over stdio

```csharp
var serverBuilder = new ServerBuilder("Demo Server", "1.0.0");

serverBuilder.Context
    .AddStdioTransport()                         // the transport (required; Build() throws if none)
    .AddNewtonsoftJson()
    .AddConsoleLogger()
    .ConfigureInfo(info =>                        // optional metadata (name/version are already set)
    {
        info.Title       = "Demo";
        info.Description = "A demo MCP server";
    })
    .AddToolsCapability(tools => tools
        .AddTool(new TestTool())                 // a tool instance you constructed
        .AddTool<WeatherTool>()                  // a tool the container constructs (ctor deps injected)
        .WithPageSize(50));                      // tools/list page size (omit for a single page)

var mcpServer = serverBuilder.Build();           // validates wiring
await mcpServer.Start();
```

### Server over Streamable HTTP

A Streamable HTTP server listens on one endpoint and serves many clients at once. Each client
connection is a **session**, and every session gets its own `McpServer`. Configuration therefore has
two surfaces:

- **`Context`** — registered once and **shared by every session**. Put the serializer, logger, and
  any stateless tools here. This is all most servers need.
- **`ConfigureSession`** — a callback on the transport options that runs **once per session**. Its
  `session` argument gives you a per-session `Context` (for tools or state unique to that
  connection) plus the connection's identity (`SessionId`, `Origin`), so behavior can vary per client.

Register tools on `Context` unless a tool must hold per-connection state or be shown to only some
clients — a tool's definition is identical for every client, and tool handlers are normally stateless.

> `baseUrl` and `path` are separate. `baseUrl` is the host:port to bind (`http://localhost:3000`);
> `path` is the endpoint route (`/mcp`). Don't put the path in the base url.

```csharp
var serverBuilder = new ServerBuilder("Demo Server", "1.0.0");

// Shared by every session
serverBuilder.Context
    .AddNewtonsoftJson()
    .AddConsoleLogger()
    .ConfigureInfo(info => info.Title = "Demo")
    .AddToolsCapability(tools => tools.AddTool(new EchoTool()));

// The transport. Its ConfigureSession callback runs once per session;
// session.Context is that session's own registrations.
serverBuilder.Context.AddStreamableHttpTransport(
    "http://localhost:3000",                     // baseUrl: host:port to bind
    "/mcp",                                      // path: endpoint route
    http => http.ConfigureSession(session =>
    {
        session.Context.AddToolsCapability(tools => tools.AddTool(new CartTool()));   // per-connection state
        if (session.Origin == "https://admin.example.com")                            // shown only to this origin
            session.Context.AddToolsCapability(tools => tools.AddTool(new AdminTool()));
    }));

var httpServer = serverBuilder.Build();
await httpServer.Start();                         // accepts connections; one McpServer per session
```

### Client over stdio

```csharp
var clientBuilder = new ClientBuilder("Echo client", "1.0.0");

clientBuilder.Context
    .AddStdioTransport("echo")                   // command to launch (+ optional arguments)
    .AddNewtonsoftJson()
    .AddConsoleLogger()
    .AddSamplingCapability(new MySamplingController())
    .AddRootsCapability(new MyRootsController());

var client = clientBuilder.Build();
await client.Connect();
```

### Client over Streamable HTTP

The client takes the single, full endpoint url (the address it POSTs to — base url plus path).

```csharp
var clientBuilder = new ClientBuilder("Echo client", "1.0.0");

clientBuilder.Context
    .AddStreamableHttpTransport("http://localhost:3000/mcp")
    .AddNewtonsoftJson()
    .AddConsoleLogger();

var httpClient = clientBuilder.Build();
await httpClient.Connect();
```

---

## Part 2 — API Surface

### Entry points

There is one builder per role, each in the core library. Name and version are **required constructor
parameters**; the transport and everything else are added afterwards on `Context`. There is no static
`CreateBuilder` factory — you construct the builder directly, which keeps the same compile-time
guarantee (the code won't compile without name and version) and avoids a second public `McpServer` /
`McpClient` symbol colliding with the runtime classes of those names.

```csharp
var serverBuilder = new ServerBuilder("Demo Server", "1.0.0");   // McpSdk.Server
var clientBuilder = new ClientBuilder("Echo client", "1.0.0");   // McpSdk.Client
```

### Builders

Builders are thin; all real configuration — including the transport — flows through `Context`. Every
transport returns the same builder type, so there is no per-transport builder.

```csharp
public sealed class ServerBuilder
{
    public ServerBuilder(string name, string version);
    public IContext Context { get; }
    public IServer Build();
}

public sealed class ClientBuilder
{
    public ClientBuilder(string name, string version);
    public IContext Context { get; }
    public IClient Build();
}
```

### Transports

A transport is registered on `Context` like any other feature. The address each transport requires
is a parameter of its registration method. `StdioTransport` lives in the core libraries
(`McpSdk.Server` / `McpSdk.Client`); the Streamable HTTP transports live in their adapter assemblies,
so the core stays free of transport dependencies.

```csharp
// Server — stdio (McpSdk.Server). Binds the process's stdin/stdout.
public static class StdioServerTransportExtensions
{
    public static IContext AddStdioTransport(this IContext c);
}

// Server — Streamable HTTP (McpSdk.Adapter.StreamableHttpServer)
public static class StreamableHttpServerTransportExtensions
{
    public static IContext AddStreamableHttpTransport(
        this IContext c, string baseUrl, string path, Action<IStreamableHttpServerOptions> configure = null);
}

// Client — stdio (McpSdk.Client). Launches the server as a subprocess.
public static class StdioClientTransportExtensions
{
    public static IContext AddStdioTransport(this IContext c, string command, params string[] args);
}

// Client — Streamable HTTP (McpSdk.Adapter.StreamableHttpClient)
public static class StreamableHttpClientTransportExtensions
{
    public static IContext AddStreamableHttpTransport(this IContext c, string endpointUrl);
}
```

`ConfigureSession` is HTTP-server-only, so it lives on that transport's options — the one place it is
valid — rather than on the builder.

```csharp
public interface IStreamableHttpServerOptions
{
    /// Runs once per session. session.Context is that session's own registration surface;
    /// whatever you add there exists only for that session.
    IStreamableHttpServerOptions ConfigureSession(Action<ISession> configure);
}

/// One live client connection.
public interface ISession
{
    IContext Context { get; }       // this session's own registrations
    string SessionId { get; }       // the Mcp-Session-Id issued on initialize
    string Origin { get; }          // the request Origin (null for non-browser clients)
    ITransport Transport { get; }   // this session's transport (wired up for you)
}
```

### `IContext` — shared registrations (both server and client)

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

These are only in scope with `using McpSdk.Server`, so a client app never sees them. Every
capability has the same `Add<Name>Capability(...)` shape.

```csharp
public static class ServerContextExtensions
{
    public static IContext ConfigureInfo(this IContext c, Action<ServerInfoOptions> configure);

    public static IContext AddToolsCapability(this IContext c, Action<IToolsBuilder> configure);
    public static IContext AddToolsCapability(this IContext c, IToolsController controller);   // bring your own

    public static IContext AddPromptsCapability(this IContext c, IPromptController controller);
    public static IContext AddResourcesCapability(this IContext c, IResourcesController controller);
    public static IContext AddCompletionCapability(this IContext c, ICompletionController controller);
    public static IContext AddLoggingCapability(this IContext c);   // advertises the `logging` capability
}
```

> Prompts and resources currently take a controller instance. They can gain a builder-lambda form
> (like tools) later, once they have a default controller to configure.

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
    /// Register a tool instance you constructed yourself.
    IToolsBuilder AddTool(IToolHandler handler);

    /// Register a tool the container constructs, injecting its constructor dependencies.
    /// Use this when a tool has dependencies of its own.
    IToolsBuilder AddTool<THandler>() where THandler : class, IToolHandler;

    /// tools/list page size. Omit for a single page with no pagination.
    IToolsBuilder WithPageSize(int pageSize);
}
```

### Options shapes

`Name` and `Version` are not here — they are required builder constructor parameters.

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

## Implementation note — how `Build()` selects the transport

This is about how the API is built, not part of the public surface, but it explains why one
builder type can produce structurally different servers.

`AddStdioTransport()` / `AddStreamableHttpTransport(...)` don't register a leaf service — they
register the **server host**: the object that owns the lifecycle. Internally:

- `AddStdioTransport()` registers an `IServerHost` (via `AddStdioServerHost()`) whose job is "bind
  stdin/stdout as one transport, create one `McpServer`, pump messages."
- `AddStreamableHttpTransport(...)` registers an `IServerHost` (via `AddStreamableHttpServerHost(...)`)
  whose job is "open the `HttpListener`, and for each connection create a session + transport +
  `McpServer`."

`ServerBuilder.Build()` resolves the single registered `IServerHost` and returns it as `IServer`. It
does **not** branch on transport type — the `HttpListener`-vs-stdio difference lives entirely inside
the resolved host. If no host is registered, `Build()` throws; that is the build-time transport
validation. This mirrors how ASP.NET Core picks Kestrel vs HTTP.sys: `UseKestrel()` / `UseHttpSys()`
swap the registered `IServer`, and the host just resolves it — same builder, same `Build()`,
different network stack.

To support `ConfigureSession`, the Streamable HTTP host creates a per-session **child (scoped)
container** off the shared root: it resolves each session's `McpServer` from that child, applies the
`ConfigureSession` callback to it, and falls back to the root for everything else. The shared
`Context` is the root container; `session.Context` is the child. The container today is a single flat
one, so this child/scoped-container support is the main capability the per-session model adds.

Clients are simpler: there is no listener and no per-connection fan-out, so the client transport
registration registers the connection `ITransport` directly (the stdio subprocess or the HTTP
endpoint), and `ClientBuilder.Build()` wires that single transport into the `IClient`.
