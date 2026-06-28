using McpSdk.Adapter.ConsoleLogger;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Adapter.SseServer;
using McpSdk.Protocol;
using McpSdk.Server;
using McpSdk.Server.Tests;
using McpSdk.Server.Tests.Conformance;
using McpSdk.Shared;

if (args.Length > 0 && args[0] == "conformance")
{
    var failures = await ConformanceTests.RunAll();
    Environment.Exit(failures);
    return;
}

// Child-process entry point for the stdio round-trip conformance test. Speaks MCP over stdio and
// blocks forever; the transport reserves stdout for protocol frames and sends all logging to stderr.
if (args.Length > 0 && args[0] == "stdio-server")
{
    var stdioServer = new ServerBuilder()
        .WithName("Stdio Conf Server")
        .WithVersion("1.0.0")
        .ConfigureContext(c => c
            .AddConsoleLogger()
            .AddNewtonsoftJson()
            .AddStdioTransport()
            .AddDefaultToolsCapability(tools => tools.AddTool(new TestToolHandler())))
        .Build();

    await stdioServer.Start();
    await Task.Delay(Timeout.Infinite);
    return;
}

var loggerFactory = new ServerConsoleLoggerFactory();
var sseServer = new HttpListenerSseServer(
    "http://localhost:3000", 
    "/sse", 
    "/messages",
    loggerFactory
);
sseServer.SessionStarted += async sseSession =>
{
    var mcpServer = new ServerBuilder()
        .WithName("Demo Server")
        .WithVersion("1.0.0")
        .ConfigureContext(c => c
            .AddLogger(loggerFactory)
            .AddNewtonsoftJson()
            .AddSseSession(sseSession)
            .AddSseTransport()
            .AddDefaultToolsCapability(tools =>
            {
                tools.AddTool(new TestToolHandler());
            }))
        .Build();

    await mcpServer.Start();
};

await sseServer.Start();


// var test = new StdioTests();
// await test.Run();