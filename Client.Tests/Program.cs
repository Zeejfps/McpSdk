using McpSharp.Client;
using McpSharp.Protocol;

var json = new SystemJson();
var httpSseClientFactory = new SystemSseClientFactory();

var sseTransportFactory = new SseTransportFactory(json, httpSseClientFactory);
var clientFactory = new ClientFactory(sseTransportFactory);
var client = clientFactory.CreateClient(new ClientInfo("Echo Client", "1.0.0"));

await client.Connect();

// var toolInfos = await client.ListTools();
//
// var result = await client.CallTool(
//     "echo"
// );