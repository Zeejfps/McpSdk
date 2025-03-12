using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Protocol.Models;
using McpSdk.Server;
using McpSdk.Server.Tests;

var json = new NewtonsoftJson();
var toolsController = new ToolsController(json);

toolsController.AddTool(tool =>
{
    tool.Name("get-forecast");
    tool.Description("Get weather forecast for a location");
    tool.Input("latitude", input =>
    {
        input.Number().Min(-90).Max(90).Describe("Latitude of the location");
    });
    tool.Input("longitude", input =>
    {
        input.Number().Min(-180).Max(180).Describe("Longitude of the location");
    });
    tool.Input("test", input =>
    {
        input.Array().Number().MinItems(10).MaxItems(10).Number().Describe("Testing input");
    });
}, args =>
{
    var latitude = args["latitude"].AsDouble();
    var longitude = args["longitude"].AsDouble();
    var result = new CallToolResult(json, null, false);
    return Task.FromResult(result);
});

var server = new ServerBuilder(json)
    .WithName("Demo Server")
    .WithVersion("1.0.0")
    .WithStdioTransport()
    .WithToolsCapability(toolsController)
    .Build();

await server.Start();