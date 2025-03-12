using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Protocol;
using McpSdk.Server;
using McpSdk.Server.Tests;

var json = new NewtonsoftJson();
var tools = new Tools(json);

var getForecastTool = new ToolBuilder(json)
    .Name("get-forecast")
    .Description("Get weather forecast for a location")
    .Input("latitude", input =>
    {
        input.Number().Min(-90).Max(90).Describe("Latitude of the location");
    })
    .Input("longitude", input =>
    {
        input.Number().Min(-180).Max(180).Describe("Longitude of the location");
    })
    .Build();

tools.AddTool(getForecastTool, args =>
{
    var latitude = args["latitude"].AsDouble();
    var longitude = args["longitude"].AsDouble();
    return new CallToolResult(json, null, false);
});

var server = new ServerBuilder(json)
    .WithName("Demo Server")
    .WithVersion("1.0.0")
    .WithStdioTransport()
    .WithToolsCapability(tools)
    .Build();

await server.Start();