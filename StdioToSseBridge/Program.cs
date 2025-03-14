using McpSdk.Adapter.ConsoleLogger;
using McpSdk.Adapter.SseClient;
using StdioToSseBridge;

var serverLogger = new ServerConsoleLoggerFactory();
var logger = serverLogger.Create<Program>();
var sseClientFactory = new SseClientFactory(serverLogger);
var sseClient = sseClientFactory.Create();
var bridge = new Bridge("localhost", 3000, sseClient, serverLogger);

try
{
    await bridge.Run();
}
catch (Exception e)
{
    logger.LogError(e);
}
