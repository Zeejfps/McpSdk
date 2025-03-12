using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Client.Tests;

public sealed class RootsControllerFactory : IRootsCapabilityFactory
{
    public IRootsController Create()
    {
        return new RootsController();
    }
}

internal sealed class RootsController : IRootsController
{
    public event Action? ListChanged;
    public bool IsListChangedNotificationSupported => true;
    public async Task<ListRootsResult> ListRoots()
    {
        return new ListRootsResult(Array.Empty<Root>());
    }
}