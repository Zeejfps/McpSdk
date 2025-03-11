namespace McpSdk.Server.Tests;

public sealed class ToolsCapabilityFactory : IToolsCapabilityFactory
{
    public IToolsCapability Create()
    {
        return new ToolsCapability();
    }
}