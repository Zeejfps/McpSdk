# McpSdk

### Client Example

```C#
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Client;
using McpSdk.Protocol;

var json = new NewtonsoftJson();
var rootsCapabilityFactory = new RootsCapabilityFactory(json);
var samplingCapabilityFactory = new SamplingCapabilityFactory(json);

var client = new ClientBuilder(json)
    .WithName("Echo Client")
    .WithVersion("1.0.0")
    .WithStdioTransport("bun", ["index.ts"])
    .WithRootsCapability(rootsCapabilityFactory)
    .WithSamplingCapability(samplingCapabilityFactory)
    .Build();

await client.Connect();
```

### Server Example

```C#
using McpSdk.Adapter.ConsoleLogger;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Server;
using McpSdk.Server.Tests;

var json = new NewtonsoftJson();
var mcpServer = new ServerBuilder()
    .WithName("Demo Server")
    .WithVersion("1.0.0")
    .WithConsoleLogger()
    .WithStdioTransport(json)
    .WithDefaultToolsCapability(json, tools =>
    {
        tools.AddTool(new TestTool());
    })
    .Build();

await mcpServer.Start();
```

### Streamable HTTP (2025-11-25)

A single MCP endpoint over HTTP. The listener issues an `Mcp-Session-Id` on `initialize` and serves
one `McpServer` per session; subsequent requests carry that id and the `MCP-Protocol-Version` header.
Disallowed `Origin`s are rejected with `403`, and the server→client `GET` stream supports
`Last-Event-ID` resumption.

```C#
// Server
using McpSdk.Adapter.ConsoleLogger;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Adapter.StreamableHttpServer;
using McpSdk.Server;

var json = new NewtonsoftJson();
var loggerFactory = new ServerConsoleLoggerFactory();
var listener = new StreamableHttpListener(
    "http://localhost:3000", "/mcp", json, loggerFactory,
    onSession: async transport =>
    {
        var server = new ServerBuilder()
            .WithName("Demo Server")
            .WithVersion("1.0.0")
            .WithLogger(loggerFactory)
            .WithStreamableHttpTransport(transport)
            .WithDefaultToolsCapability(json, tools => tools.AddTool(new TestTool()))
            .Build();
        await server.Start();
    });

await listener.Start();
```

```C#
// Client
using McpSdk.Adapter.ConsoleLogger;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Adapter.StreamableHttpClient;
using McpSdk.Client;

var json = new NewtonsoftJson();
var loggerFactory = new ClientConsoleLoggerFactory();
var http = new StreamableHttpClientAdapter("http://localhost:3000/mcp", loggerFactory);

var client = new ClientBuilder()
    .WithName("Echo Client")
    .WithVersion("1.0.0")
    .WithLogger(loggerFactory)
    .WithStreamableHttpTransport(json, http)
    .Build();

await client.Connect();
```
