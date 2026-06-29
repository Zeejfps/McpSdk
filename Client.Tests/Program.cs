using McpSdk.Adapter.ConsoleLogger;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Adapter.System.Net.Http;
using McpSdk.Client;
using McpSdk.Client.Tests;
using McpSdk.Client.Transports;
using McpSdk.Protocol.Models;

// Demo MCP client. Runs initialize -> tools/list -> tools/call against the server given on the
// command line. An http(s) URL uses the Streamable HTTP transport; anything else is a stdio command.
//
//   dotnet run --project Client.Tests -- http://localhost:3000/mcp
//   dotnet run --project Client.Tests -- dotnet <path>/McpSdk.Server.Tests.dll stdio-server
if (args.Length == 0)
{
    Console.Error.WriteLine("usage: McpSdk.Client.Tests <http(s)-endpoint-url | server-command [server-args...]>");
    Environment.Exit(1);
    return;
}

var target = args[0];

var json = new NewtonsoftJson();
var loggerFactory = new ClientConsoleLoggerFactory();
var rootsControllerFactory = new RootsControllerFactory();
var samplingControllerFactory = new SamplingControllerFactory();
var builder = new ClientBuilder()
    .WithName("Echo Client")
    .WithVersion("1.0.0")
    .WithLogger(loggerFactory)
    .WithRootsCapability(rootsControllerFactory)
    .WithSamplingCapability(samplingControllerFactory);

if (target.StartsWith("http://") || target.StartsWith("https://"))
{
    builder.WithStreamableHttpTransport(json, new StreamableHttpClient(target, loggerFactory));
}
else
{
    builder.WithStdioTransport(json, target, args[1..]);
}

var client = builder.Build();

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
    props.Write("testBool", true);
    props.Write("testArray", new[] { "alpha", "beta" });
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
