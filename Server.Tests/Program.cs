using McpSdk.Adapter.ConsoleLogger;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Adapter.SseServer;
using McpSdk.Server;
using McpSdk.Server.Tests;

var loggerFactory = new ServerConsoleLoggerFactory();
var json = new NewtonsoftJson();
var sseServer = new HttpListenerSseServer("/sse", "/messages", loggerFactory);
sseServer.SessionStarted += async sseSession =>
{
    var mcpServer = new ServerBuilder()
        .WithName("Demo Server")
        .WithVersion("1.0.0")
        .WithLogger(loggerFactory)
        .WithSseTransport(json, sseSession)
        .WithDefaultToolsCapability(json, tools =>
        {
            tools.AddTool(new TestTool());
        })
        .Build();

    await mcpServer.Start();
};

await sseServer.Start();
