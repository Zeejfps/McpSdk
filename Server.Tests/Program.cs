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
    var stdioJson = new NewtonsoftJson();
    var stdioServer = new ServerBuilder()
        .WithName("Stdio Conf Server")
        .WithVersion("1.0.0")
        .WithConsoleLogger()
        .ConfigureContext(c => c
            .AddSingleton<IJson>(stdioJson)
            .AddStdioTransport())
        .WithDefaultToolsCapability(stdioJson, tools => tools.AddTool(new TestToolHandler()))
        .Build();

    await stdioServer.Start();
    await Task.Delay(Timeout.Infinite);
    return;
}

var loggerFactory = new ServerConsoleLoggerFactory();
var json = new NewtonsoftJson();
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
        .WithLogger(loggerFactory)
        .ConfigureContext(c => c
            .AddSingleton<IJson>(json)
            .AddSingleton<ISseSession>(sseSession)
            .AddSseTransport())
        .WithDefaultToolsCapability(json, tools =>
        {
            tools.AddTool(new TestToolHandler());
        })
        .Build();

    await mcpServer.Start();
};

await sseServer.Start();


// var test = new StdioTests();
// await test.Run();