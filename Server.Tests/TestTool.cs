using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server.Tests;

public sealed class TestTool : ITool
{
    public Tool Info { get; }

    public TestTool()
    {
        Info = new Tool(
            "get-forecast",
            "asdawdawd",
            new ObjectSchema
            {
                {
                    "latitude", new NumberSchema
                    {
                        Minimum = -90.0,
                        Maximum = 90.0,
                        Description = "Latitude of the location"
                    }
                },
                {
                    "longitude", new NumberSchema
                    {
                        Minimum = -180.0,
                        Maximum = 180.0,
                        Description = "Longitude of the location"
                    }
                },
                {
                    "testBool", new BooleanSchema()
                },
                {
                    "testArray", new ArraySchema("string")
                    {
                        MinItems = 0,
                        MaxItems = 10,
                    }
                }
            })
        {
            Title = "Weather Forecast",
            Annotations = new ToolAnnotations
            {
                Title = "Get Forecast",
                ReadOnlyHint = true,
                OpenWorldHint = true,
            },
            Icons =
            [
                new Icon("https://example.com/forecast.png", "image/png", "48x48"),
            ],
        };
    }
    
    public Task<CallToolResult> Call(IJsonObject args)
    {
        var latitude = args["latitude"].AsDouble();
        var longitude = args["longitude"].AsDouble();
        var content = new TextContent($"Lat: {latitude}, Long: {longitude}");
        var result = new CallToolResult([content], false);
        return Task.FromResult(result);
    }
}