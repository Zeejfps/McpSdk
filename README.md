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
using McpSdk.Adapter.SseServer;
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
