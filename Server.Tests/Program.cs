using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Protocol.Models;
using McpSdk.Server;

var json = new NewtonsoftJson();
var server = new ServerBuilder(json)
    .WithName("Demo Server")
    .WithVersion("1.0.0")
    .WithStdioTransport()
    .WithToolsCapability(tools =>
    {
        tools.AddTool(toolWriter =>
        {
            toolWriter
                .WriteName("get-forecast")
                .WriteDescription("asdawdawd")
                .WriteInputSchema(inputSchemaWriter =>
                {
                    inputSchemaWriter
                        .Prop("latitude", propWriter =>
                        {
                            propWriter.Number().Min(-90).Max(90).Describe("Latitude of the location");
                        })
                        .Prop("longitude", propWriter =>
                        {
                            propWriter.Number().Min(-90).Max(90).Describe("Longitude of the location");
                        });
                });
        }, args =>
        {
            return null;
        });
        
        tools.AddTool(tool =>
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
            var content = new TextContent(json, $"Lat: {latitude}, Long: {longitude}");
            var result = new CallToolResult(json, [content], false);
            return Task.FromResult(result);
        });
    })
    .Build();

await server.Start();