namespace McpSdk.Server.Tests;

public sealed class TestToolFactory : IToolFactory
{
    public ITool Create()
    {
        return new TestTool();
    }
}