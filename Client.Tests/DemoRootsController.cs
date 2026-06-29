using McpSdk.Protocol.Models;

namespace McpSdk.Client.Tests;

internal sealed class DemoRootsController : IRootsController
{
    public event Action? ListChanged;
    public bool IsListChangedNotificationSupported => true;

    public async Task<ListRootsResult> ListRoots()
    {
        return new ListRootsResult(Array.Empty<Root>());
    }
}
