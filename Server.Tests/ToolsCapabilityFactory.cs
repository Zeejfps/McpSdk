using McpSdk.Protocol;

namespace McpSdk.Server.Tests;

public sealed class ToolsCapabilityFactory : IToolsCapabilityFactory
{
    private readonly IJson _json;

    public ToolsCapabilityFactory(IJson json)
    {
        _json = json;
    }

    public IToolsCapability Create()
    {
        return new Tools(_json);
    }
}