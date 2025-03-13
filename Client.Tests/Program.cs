using Adapter.ConsoleLogger;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Adapter.SseClient;
using McpSdk.Client;
using McpSdk.Client.Tests;
using McpSdk.Protocol.Models;

var json = new NewtonsoftJson();//new SystemJson();
var sseClientFactory = new SseClientFactory();
var rootsControllerFactory = new RootsControllerFactory();
var samplingControllerFactory = new SamplingControllerFactory();
var loggerFactory = new ClientConsoleLoggerFactory();
var client = new ClientBuilder(json)
    .WithName("Echo Client")
    .WithVersion("1.0.0")
    .WithLogger(loggerFactory)
    .WithSseTransport(sseClientFactory, "http://localhost:3000")
    //.WithStdioTransport("G:\\Dev\\C#\\MCPSharp\\Server.Tests\\bin\\Debug\\net9.0\\McpSdk.Server.Tests.exe", [])
    .WithRootsCapability(rootsControllerFactory)
    .WithSamplingCapability(samplingControllerFactory)
    .Build();

await client.Connect();

var listToolsResult = await client.ListTools();
Console.WriteLine("Available tools:");
foreach (var tool in listToolsResult.Tools)
{
    Console.WriteLine(json.Stringify(tool.AsJson));
}

var request = new CallToolRequest("get-forecast", json.Object(props =>
{
    props.Write("latitude", 39.384358225955);
    props.Write("longitude", -110.686663445063);
}));
var result = await client.CallTool(request);

Console.WriteLine(json.Stringify(request.AsJson));
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
