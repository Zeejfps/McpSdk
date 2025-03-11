using McpSharp.Server;
using Server.Tests;

var testToolFactory = new TestToolFactory();

var server = new ServerBuilder()
    .WithName("Demo Server")
    .WithVersion("1.0.0")
    .WithStdioTransport()
    .WithTool(testToolFactory)
    .Build();
    
await server.Start();