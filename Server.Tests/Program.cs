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
                        .Number("latitude", z =>
                        {
                            z.Min(-90).Max(90).Describe("Latitude of the location");
                        })
                        .Number("longitude", z =>
                        {
                            z.Min(-180).Max(180).Describe("Longitude of the location");
                        })
                        .Boolean("testBool", boolWriter => { })
                        .Array("testArray", arrayWriter =>
                        {
                            arrayWriter.MinItems(0).MaxItems(10).Boolean();
                        });
                });
        }, args =>
        {
            var latitude = args["latitude"].AsDouble();
            var longitude = args["longitude"].AsDouble();
            var content = new TextContent(json, $"Lat: {latitude}, Long: {longitude}");
            var result = new CallToolResult([content], false);
            return Task.FromResult(result);
        });
    })
    .Build();

await server.Start();
