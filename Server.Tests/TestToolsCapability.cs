using McpSdk.Protocol;

namespace McpSdk.Server.Tests;

public class TestToolsCapability : IToolsCapability
{
    public Task<ListToolsResult> ListTools()
    {
        throw new NotImplementedException();
    }

    public Task<CallToolResult> CallTool(CallToolArguments arguments)
    {
        throw new NotImplementedException();
    }
}