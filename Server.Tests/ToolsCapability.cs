using McpSdk.Protocol;

namespace McpSdk.Server.Tests;

public class ToolsCapability : IToolsCapability
{
    private readonly IJson _json;
    private readonly Dictionary<string, Tool> _toolByNameLookup = new();
    
    public ToolsCapability(IJson json)
    {
        _json = json;
        
        var tool = new ToolBuilder(_json)
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
        
        _toolByNameLookup.Add(tool.Name, tool);
    }

    public bool IsListChangedNotificationSupported => false;

    public Task<ListToolsResult> ListTools()
    {
        var result = new ListToolsResult(_json, _toolByNameLookup.Values.ToArray());
        return Task.FromResult(result);
    }

    public Task<CallToolResult> CallTool(CallToolArguments arguments)
    {
        var tool = _toolByNameLookup[arguments.ToolName];
        // _json.ValidateAgainstSchema(arguments.JsonObject, tool.InputSchema);
        throw new NotImplementedException();
    }
}