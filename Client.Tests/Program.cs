using McpSdk.Adapter.ConsoleLogger;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Adapter.SseClient;
using McpSdk.Client;
using McpSdk.Client.Tests;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Shared;

var json = new NewtonsoftJson();//new SystemJson();
var loggerFactory = new ClientConsoleLoggerFactory();
var sseClientFactory = new SseClientFactory(
    "http://localhost:3000", 
    "/sse",
    loggerFactory
);
var rootsControllerFactory = new RootsControllerFactory();
var samplingControllerFactory = new SamplingControllerFactory();
var client = new ClientBuilder()
    .WithName("Echo Client")
    .WithVersion("1.0.0")
    .ConfigureContext(c => c
        .AddLogger(loggerFactory)
        .AddSingleton<IJson>(json)
        .AddSingleton<ISseClientFactory>(sseClientFactory)
        .AddSseTransport()
        //.AddStdioTransport("G:\\Dev\\C#\\MCPSharp\\Server.Tests\\bin\\Debug\\net9.0\\McpSdk.Server.Tests.exe", [])
        .AddRootsCapability(rootsControllerFactory)
        .AddSamplingCapability(samplingControllerFactory))
    .Build();

await client.Connect();

var listToolsResult = await client.ListTools();
Console.WriteLine("Available tools:");
foreach (var tool in listToolsResult.Tools)
{
    Console.WriteLine(json.Stringify(tool.WriteMembers));
}

var request = new CallToolRequest("get-forecast", json.Object(props =>
{
    props.Write("latitude", 39.384358225955);
    props.Write("longitude", -110.686663445063);
}));
var result = await client.CallTool(request);

Console.WriteLine(json.Stringify(request.WriteMembers));
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
