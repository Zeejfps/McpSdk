using McpSharp.Server;

var server = new ServerBuilder()
    .WithName("Demo Server")
    .WithVersion("1.0.0")
    .WithStdioTransport()
    .Build();
    
await server.Start();