using McpSdk.Adapter.ConsoleLogger;
using McpSdk.Adapter.SseClient;
using StdioToSseBridge;

var serverLogger = new ServerConsoleLoggerFactory();
var logger = serverLogger.Create<Program>();
var baseUrl = "http://localhost:3000";
var sseClientFactory = new SseClientFactory(baseUrl, "/sse", serverLogger);
var sseClient = sseClientFactory.Create();
var bridge = new Bridge(sseClient, serverLogger);

try
{
    await bridge.Run();
}
catch (Exception e)
{
    logger.LogError(e);
}
