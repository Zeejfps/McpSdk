using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Server;
using McpSdk.Server.Tests;

var json = new NewtonsoftJson();
var testToolFactory = new ToolsCapabilityFactory(json);

var server = new ServerBuilder(json)
    .WithName("Demo Server")
    .WithVersion("1.0.0")
    .WithStdioTransport()
    .WithToolsCapability(testToolFactory)
    .Build();
    
await server.Start();