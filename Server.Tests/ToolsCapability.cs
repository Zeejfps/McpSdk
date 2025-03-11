using McpSdk.Protocol;

namespace McpSdk.Server.Tests;

public class ToolsCapability : IToolsCapability
{
    public bool IsListChangedNotificationSupported => false;

    public Task<ListToolsResult> ListTools()
    {
        var tool = new ToolBuilder(null)
            .Name("get-forecast")
            .Description("Get weather forecast for a location")
            .Build();

        var result = new ListToolsResult(null, [tool]);
        return Task.FromResult(result);
    }

    public Task<CallToolResult> CallTool(CallToolArguments arguments)
    {
        throw new NotImplementedException();
    }
}