using McpSdk.Adapter.ConsoleLogger;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Client;
using McpSdk.Client.Tests;
using McpSdk.Protocol.Models;

// Demo stdio MCP client. Spawns the MCP server given on the command line, then runs
// initialize -> tools/list -> tools/call over stdio.
//
//   dotnet run --project Client.Tests -- <server-command> [server-args...]
//
// e.g. against the sibling test server:
//   dotnet run --project Client.Tests -- dotnet <path>/McpSdk.Server.Tests.dll stdio-server
if (args.Length == 0)
{
    Console.Error.WriteLine("usage: McpSdk.Client.Tests <server-command> [server-args...]");
    Environment.Exit(1);
    return;
}

var command = args[0];
var serverArgs = args[1..];

var json = new NewtonsoftJson();
var loggerFactory = new ClientConsoleLoggerFactory();
var rootsControllerFactory = new RootsControllerFactory();
var samplingControllerFactory = new SamplingControllerFactory();
var client = new ClientBuilder()
    .WithName("Echo Client")
    .WithVersion("1.0.0")
    .WithLogger(loggerFactory)
    .WithStdioTransport(json, command, serverArgs)
    .WithRootsCapability(rootsControllerFactory)
    .WithSamplingCapability(samplingControllerFactory)
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
