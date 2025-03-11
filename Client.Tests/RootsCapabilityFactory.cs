using McpSharp.Client;

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
    public Task<IListRootsResult> ListRoots()
    {
        throw new NotImplementedException();
    }
}