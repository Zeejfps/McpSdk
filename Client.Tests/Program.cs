using Client.Tests;
using McpSharp.Client;
using McpSharp.Protocol;
using SseClientAdapter;

var json = new SystemJson();
var sseClientFactory = new SseClientFactory();
var rootsCapabilityFactory = new RootsCapabilityFactory(json);
var samplingCapabilityFactory = new SamplingCapabilityFactory(json);

var client = new ClientBuilder(json)
    .WithName("Echo Client")
    .WithVersion("1.0.0")
    .WithSseTransport(sseClientFactory, "http://localhost:3000")
    .WithRootsCapability(rootsCapabilityFactory)
    .WithSamplingCapability(samplingCapabilityFactory)
    .Build();

await client.Connect();

var listToolsResult = await client.ListTools();
Console.WriteLine("Available tools:");
foreach (var tool in listToolsResult.Tools)
{
    Console.WriteLine(tool.ToString());
}

var result = await client.CallTool(
    "get-forecast",
    toolArgs =>
    {
        toolArgs.Write("latitude", 51.5);
        toolArgs.Write("longitude", 51.5);
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
