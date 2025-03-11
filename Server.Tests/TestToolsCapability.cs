using McpSdk.Protocol;

namespace McpSdk.Server.Tests;

public class TestToolsCapability : IToolsCapability
{
    public Task<ListToolsResult> ListTools()
    {
        throw new NotImplementedException();
    }

    public Task<CallToolResult> CallTool(string toolName)
    {
        throw new NotImplementedException();
    }
}