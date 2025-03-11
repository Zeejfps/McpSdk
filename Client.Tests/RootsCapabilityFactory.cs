using McpSharp.Client;
using McpSharp.Protocol;

namespace Client.Tests;

public sealed class RootsCapabilityFactory : IRootsCapabilityFactory
{
    public IRootsCapability Create()
    {
        return new RootsCapability();
    }
}

internal sealed class RootsCapability : IRootsCapability
{
    public event Action? ListChanged;
    public bool IsListChangedNotificationSupported => true;
    public Task<ListRootsResult> ListRoots()
    {
        throw new NotImplementedException();
    }
}