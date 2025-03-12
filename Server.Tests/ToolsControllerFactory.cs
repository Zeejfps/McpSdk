using McpSdk.Protocol;

namespace McpSdk.Server.Tests;

public sealed class ToolsControllerFactory : IToolsControllerFactory
{
    private readonly IJson _json;

    public ToolsControllerFactory(IJson json)
    {
        _json = json;
    }

    public IToolsController Create()
    {
        return new ToolsController(_json);
    }
}