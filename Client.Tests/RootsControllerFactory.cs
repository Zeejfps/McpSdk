using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Client.Tests;

public sealed class RootsControllerFactory : IRootsCapabilityFactory
{
    private readonly IJson _json;

    public RootsControllerFactory(IJson json)
    {
        _json = json;
    }

    public IRootsController Create()
    {
        return new RootsController(_json);
    }
}

internal sealed class RootsController : IRootsController
{
    private readonly IJson _json;

    public RootsController(IJson json)
    {
        _json = json;
    }

    public event Action? ListChanged;
    public bool IsListChangedNotificationSupported => true;
    public async Task<ListRootsResult> ListRoots()
    {
        return new ListRootsResult(_json, Array.Empty<Root>());
    }
}