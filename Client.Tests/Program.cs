using McpSharp.Client;
using McpSharp.Protocol;

var json = new SystemJson();
var httpClientFactory = new SystemHttpClientFactory(json);

var clientFactory = new ClientFactory(json, httpClientFactory);
var client = clientFactory.CreateClient(new ClientInfo("Echo Client", "1.0.0"));

await client.Connect();

// var toolInfos = await client.ListTools();
//
// var result = await client.CallTool(
//     "echo"
// );