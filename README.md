# McpSdk

A lean, zero-dependency C# SDK for the [Model Context Protocol](https://modelcontextprotocol.io)
— both client and server.

## Configuration: the `IContext` container

`ServerBuilder` and `ClientBuilder` expose a small dependency-injection container —
`IContext`, modeled on Microsoft's `IServiceCollection`. You register services
(transport, JSON, logger, capabilities) with `Add…` extension methods through
`ConfigureContext`, and the builder resolves them — using reflection-based constructor
injection — when you call `Build()`.

Identity stays on the builder itself: `WithName` / `WithVersion` / `WithTitle` /
`WithDescription`.

```csharp
new ServerBuilder()
    .WithName("Demo").WithVersion("1.0.0")
    .ConfigureContext(c => c
        .AddConsoleLogger()
        .AddNewtonsoftJson()
        .AddStdioTransport()
        .AddDefaultToolsCapability(tools => tools.AddTool(new MyTool())))
    .Build();
```

Components register themselves and pull their own dependencies from the container, so you
don't hand-wire `IJson`, the logger, or the transport. Registration order inside
`ConfigureContext` doesn't matter — everything is resolved at `Build()`.

### Available registrations

| Extension | Registers | Package |
|---|---|---|
| `AddNewtonsoftJson()` / `AddSystemTextJson()` | `IJson` | `Adapter.Newtonsoft.Json` / `Adapter.System.Text.Json` |
| `AddStdioTransport()` *(server)* · `AddStdioTransport(command, args)` *(client)* | `ITransportFactory` | `Server` / `Client` |
| `AddSseTransport()` | `ITransportFactory` | `Server` / `Client` |
| `AddSseSession(session)` *(server)* | the per-connection session as `IOptions<SseSessionOptions>` | `Server` |
| `AddSseClient(baseUrl, endpoint)` *(client)* | `ISseClientFactory` | `Adapter.SseClient` |
| `AddConsoleLogger()` · `AddLogger(factory)` | `ILoggerFactory` | `Adapter.ConsoleLogger` / `Shared` |
| `AddDefaultToolsCapability(configure)` · `AddToolsCapability` · `AddPromptsCapability` · `AddResourcesCapability` | server capabilities | `Server` |
| `AddRootsCapability` · `AddSamplingCapability` · `AddElicitationCapability` | client capabilities | `Client` |

## Server — stdio

```csharp
using McpSdk.Adapter.ConsoleLogger;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Server;

var server = new ServerBuilder()
    .WithName("Demo Server")
    .WithVersion("1.0.0")
    .ConfigureContext(c => c
        .AddConsoleLogger()
        .AddNewtonsoftJson()
        .AddStdioTransport()
        .AddDefaultToolsCapability(tools => tools.AddTool(new MyTool())))
    .Build();

await server.Start();
```

## Server — SSE (HTTP)

A fresh server is built per connection; the per-connection session is supplied with
`AddSseSession`.

```csharp
using McpSdk.Adapter.ConsoleLogger;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Adapter.SseServer;
using McpSdk.Server;

var loggerFactory = new ServerConsoleLoggerFactory();
var sseServer = new HttpListenerSseServer("http://localhost:3000", "/sse", "/messages", loggerFactory);

sseServer.SessionStarted += async session =>
{
    var server = new ServerBuilder()
        .WithName("Demo Server")
        .WithVersion("1.0.0")
        .ConfigureContext(c => c
            .AddLogger(loggerFactory)
            .AddNewtonsoftJson()
            .AddSseSession(session)
            .AddSseTransport()
            .AddDefaultToolsCapability(tools => tools.AddTool(new MyTool())))
        .Build();

    await server.Start();
};

await sseServer.Start();
```

## Client — stdio

```csharp
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Client;

var client = new ClientBuilder()
    .WithName("Echo Client")
    .WithVersion("1.0.0")
    .ConfigureContext(c => c
        .AddNewtonsoftJson()
        .AddStdioTransport("bun", new[] { "index.ts" })
        .AddRootsCapability(new MyRootsCapabilityFactory())
        .AddSamplingCapability(new MySamplingCapabilityFactory()))
    .Build();

await client.Connect();
```

## Client — SSE (HTTP)

```csharp
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Adapter.SseClient;
using McpSdk.Client;

var client = new ClientBuilder()
    .WithName("Echo Client")
    .WithVersion("1.0.0")
    .ConfigureContext(c => c
        .AddNewtonsoftJson()
        .AddSseClient("http://localhost:3000", "/sse")
        .AddSseTransport())
    .Build();

await client.Connect();
```

## Registering your own services

The container supports instance, factory, and reflection-activated registrations, plus the
`IOptions<T>` pattern for configuration values:

```csharp
builder.Context.AddSingleton<IMyService>(new MyService());                                  // instance
builder.Context.AddSingleton<IMyService>(sp => new MyService(sp.GetRequiredService<IJson>())); // factory
builder.Context.AddSingleton<IMyService, MyService>();                                      // ctor injection
builder.Context.AddTransient<IMyService, MyService>();                                      // new per resolve

builder.Context.Configure<MyOptions>(o => o.Endpoint = "https://…");                        // IOptions<MyOptions>
```

`AddSingleton<TService, TImpl>()` activates `TImpl` by reflecting its greediest
constructor and resolving each parameter from the container. An adapter typically ships a
single `Add…(this IContext)` extension that registers itself and pulls its dependencies
from the container.
