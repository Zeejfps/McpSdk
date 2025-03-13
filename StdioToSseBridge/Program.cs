using Adapter.ConsoleLogger;
using McpSdk.Adapter.SseClient;
using StdioToSseBridge;

var serverLogger = new ServerConsoleLoggerFactory();
var sseClientFactory = new SseClientFactory();
var sseClient = sseClientFactory.Create();
var bridge = new Bridge(sseClient, serverLogger);
await bridge.Run();