using Client.Tests;
using McpSharp.Client;
using McpSharp.Protocol;

var json = new SystemJson();
var sseClientFactory = new SseClientFactory();
var sseTransportFactory = new SseTransportFactory(json, sseClientFactory, "http://localhost:3000");
var clientFactory = new ClientFactory(sseTransportFactory);
var client = clientFactory.Create(new ClientInfo("Echo Client", "1.0.0"));

await client.Connect();

var toolInfos = await client.ListTools();
Console.WriteLine("Available tools:");
foreach (var tool in toolInfos)
{
    Console.WriteLine(tool.ToString());
}

var result = await client.CallTool(
    "get-forecast",
    args =>
    {
        args.Write("latitude", 51.5);
        args.Write("longitude", 51.5);
    }
);

Console.WriteLine(result);
var contents = result["content"].AsObjectArray();
Console.WriteLine(contents.Length);

foreach (var content in contents)
{
    var type = content["type"].AsString();
    if (type == "text")
    {
        var text = content["text"].AsString();
        Console.WriteLine(text);
    }
}
