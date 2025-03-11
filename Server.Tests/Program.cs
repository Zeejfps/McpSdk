using McpSdk.Server;
using McpSdk.Server.Tests;

var testToolFactory = new TestToolsCapabilityFactory();

var server = new ServerBuilder()
    .WithName("Demo Server")
    .WithVersion("1.0.0")
    .WithStdioTransport()
    .WithToolsCapability(testToolFactory)
    .Build();
    
await server.Start();