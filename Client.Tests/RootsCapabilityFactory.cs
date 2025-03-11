using McpSharp.Client;
using McpSharp.Protocol;

namespace Client.Tests;

public sealed class RootsCapabilityFactory : IRootsCapabilityFactory
{
    private readonly IJson _json;

    public RootsCapabilityFactory(IJson json)
    {
        _json = json;
    }

    public IRootsCapability Create()
    {
        return new RootsCapability(_json);
    }
}

internal sealed class RootsCapability : IRootsCapability
{
    private readonly IJson _json;

    public RootsCapability(IJson json)
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