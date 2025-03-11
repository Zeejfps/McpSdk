namespace McpSdk.Server.Tests;

public sealed class TestToolsCapabilityFactory : IToolsCapabilityFactory
{
    public IToolsCapability Create()
    {
        return new TestToolsCapability();
    }
}