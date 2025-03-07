using McpSharp.Client;
using McpSharp.Protocol;

var clientFactory = new ClientFactory();
var client = clientFactory.CreateClient(new ClientInfo("Echo Client", "1.0.0"));

await client.Connect();

// var toolInfos = await client.ListTools();
//
// var result = await client.CallTool(
//     "echo"
// );
