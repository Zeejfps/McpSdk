using Adapter.ConsoleLogger;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Adapter.SseServer;
using McpSdk.Server;
using McpSdk.Server.Tests;

var loggerFactory = new ServerConsoleLoggerFactory();
var logger = loggerFactory.Create<Program>();
var json = new NewtonsoftJson();
var sseServer = new HttpListenerSseServer("/sse", "/messages", loggerFactory);
sseServer.SessionStarted += async session =>
{
    logger.LogDebug("Session started...");
    var mcpServer = new ServerBuilder(json)
        .WithName("Demo Server")
        .WithVersion("1.0.0")
        .WithLogger(loggerFactory)
        .WithSseTransport(session)
        .WithToolsCapability(tools =>
        {
            tools.AddTool(new TestTool());
        })
        .Build();

    await mcpServer.Start();
};

logger.LogDebug("Starting SSE Server...");
await sseServer.Start();
