using McpSharp.Protocol;
using McpSharp.Server;

namespace Server.Tests;

public class TestTool : ITool
{
    public Task<CallToolResult> Call()
    {
        throw new NotImplementedException();
    }
}