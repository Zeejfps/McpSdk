using McpSdk.Adapter.ConsoleLogger;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Adapter.SseClient;
using McpSdk.TransportBridge;
using StdioTransport = McpSdk.Server.StdioTransport;
using SseTransport = McpSdk.Client.SseTransport;

var serverLogger = new ServerConsoleLoggerFactory();
var baseUrl = "http://localhost:3000";
var sseClientFactory = new SseClientFactory(baseUrl, "/sse", serverLogger);
var sseClient = sseClientFactory.Create();
var json = new NewtonsoftJson();
var stdioTransport = new StdioTransport(json, serverLogger);
var sseTransport = new SseTransport(sseClient, json, serverLogger);
var bridge = new McpTransportBridge(serverLogger, stdioTransport, sseTransport);

await bridge.Start();

while (true)
{
    await Task.Delay(2000);
}