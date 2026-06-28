# MCP SDK — Target Public API

This describes the **desired** construction API for the SDK: how an application builds an MCP server
or client. It is a design goal, not yet implemented, and it documents *what* the API should look
like — not how it will be built internally.

The SDK uses a small dependency-injection container behind an `IContext` registration surface
(modeled on `Microsoft.Extensions.DependencyInjection`). Every builder exposes that `Context`, and
adapters contribute features as `Context.AddX(...)` extension methods.

## Design rules

- **Required values** (name, version, transport address) are **factory parameters** — you can't
  forget them, the code won't compile without them.
- **Optional metadata** (title, description) goes through `ConfigureInfo(...)`.
- **Everything else** — serializer, logger, capabilities — is registered on `Context.AddX(...)`.
  Every capability follows the same `Add<Name>Capability(...)` shape, so learning one teaches the rest.
- `Build()` is synchronous and validates the wiring (a missing serializer throws here, not later).
  `Start()` / `Connect()` are asynchronous (they do I/O).

---

## Part 1 — Usage

### Server over stdio

```csharp
var serverBuilder = StdioMcpServer.CreateBuilder("Demo Server", "1.0.0");

serverBuilder.Context
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
- **`ConfigureSession`** — a callback that runs **once per session**. Its `session` argument gives
  you a per-session `Context` (for tools or state unique to that connection) plus the connection's
  identity (`SessionId`, `Origin`), so behavior can vary per client.

Register tools on `Context` unless a tool must hold per-connection state or be shown to only some
clients — a tool's definition is identical for every client, and tool handlers are normally stateless.

> `baseUrl` and `path` are separate. `baseUrl` is the host:port to bind (`http://localhost:3000`);
> `path` is the endpoint route (`/mcp`). Don't put the path in the base url.

```csharp
var httpServerBuilder = StreamableHttpMcpServer.CreateBuilder(
    "Demo Server", "1.0.0",
    "http://localhost:3000",                     // baseUrl: host:port to bind
    "/mcp");                                     // path: endpoint route

// Shared by every session
httpServerBuilder.Context
    .AddNewtonsoftJson()
    .AddConsoleLogger()
    .ConfigureInfo(info => info.Title = "Demo")
    .AddToolsCapability(tools => tools.AddTool(new EchoTool()));

// Runs once per session; session.Context is that session's own registrations
httpServerBuilder.ConfigureSession(session =>
{
    session.Context.AddToolsCapability(tools => tools.AddTool(new CartTool()));   // per-connection state
    if (session.Origin == "https://admin.example.com")                            // shown only to this origin
        session.Context.AddToolsCapability(tools => tools.AddTool(new AdminTool()));
});

var httpServer = httpServerBuilder.Build();
await httpServer.Start();                         // accepts connections; one McpServer per session
```

### Client over stdio

```csharp
var clientBuilder = StdioMcpClient.CreateBuilder(
    "Echo client", "1.0.0",
    "echo");                                     // command to launch (+ optional arguments)

clientBuilder.Context
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

There is one factory per transport, and each takes exactly the arguments that transport requires.
`StdioMcp*` live in the core libraries (`McpSdk.Server` / `McpSdk.Client`); `StreamableHttp*` live in
their adapter assemblies, so the core stays free of transport dependencies.

```csharp
public static class StdioMcpServer
{
    public static ServerBuilder CreateBuilder(string name, string version);
}

public static class StreamableHttpMcpServer        // McpSdk.Adapter.StreamableHttpServer
{
    // Returns a StreamableHttpServerBuilder (not ServerBuilder) — it owns ConfigureSession.
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

Builders are thin; all real configuration flows through `Context`.

```csharp
public sealed class ServerBuilder
{
    public IContext Context { get; }               // transport is pre-registered by the factory
    public IServer Build();
}

public sealed class ClientBuilder
{
    public IContext Context { get; }
    public IClient Build();
}
```

The Streamable HTTP server has its own builder type, because only this transport has per-session
sessions — so `ConfigureSession` lives only here.

```csharp
public sealed class StreamableHttpServerBuilder
{
    public IContext Context { get; }                       // shared by all sessions

    /// Runs once per session. session.Context is that session's own registration surface;
    /// whatever you add there exists only for that session.
    public StreamableHttpServerBuilder ConfigureSession(Action<ISession> configure);

    public IServer Build();
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

`Name` and `Version` are not here — they are required `CreateBuilder` parameters.

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

## Implementation note

This document is about the desired API, not how it is built. One thing the API implies, though: to
support `ConfigureSession`, the `IContext` / container implementation will probably need to support
**child (scoped) containers** — a per-session container that resolves its own registrations and
falls back to the shared root container for everything else. The container today is a single flat
one, so this is the main capability the per-session model adds.
