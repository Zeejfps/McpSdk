using Client.Tests;
using McpSharp.Client;
using McpSharp.Protocol;

var json = new SystemJson();
var sseClientFactory = new SseClientFactory();
var rootsCapabilityFactory = new RootsCapabilityFactory();
var samplingCapabilityFactory = new SamplingCapabilityFactory();
var sseTransportFactory = new SseTransportFactory(json, sseClientFactory, "http://localhost:3000");
var clientFactory = new ClientFactory(sseTransportFactory);

var client = clientFactory.Create(new ClientInfo("Echo Client", "1.0.0"));

// var client = new ClientFactory()
//     .WithName("Echo Client")
//     .WithVersion("1.0.0")
//     .WithTransport(sseTransportFactory)
//     .WithRootsCapability(rootsCapabilityFactory)
//     .WithSamplingCapability(samplingCapabilityFactory)
//     .Build();

await client.Connect();

var listToolsResult = await client.ListTools();
Console.WriteLine("Available tools:");
foreach (var tool in listToolsResult.Tools)
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
var contents = result.Content;
Console.WriteLine(contents.Length);

foreach (var content in contents)
{
    if (content is TextContent textContent)
    {
        Console.WriteLine(textContent.Text);
    }
    else if (content is ImageContent imageContent)
    {
        Console.WriteLine(imageContent.MimeType);
    }
}
