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
                    "latitude", new NumberInput
                    {
                        Minimum = -90.0,
                        Maximum = 90.0,
                        Description = "Latitude of the location"
                    }
                },
                {
                    "longitude", new NumberInput
                    {
                        Minimum = -180.0,
                        Maximum = 180.0,
                        Description = "Longitude of the location"
                    }
                },
                {
                    "testBool", new BooleanInput()
                },
                {
                    "testArray", new ArrayInput("string")
                    {
                        MinItems = 0,
                        MaxItems = 10,
                    }
                }
            }
        );
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