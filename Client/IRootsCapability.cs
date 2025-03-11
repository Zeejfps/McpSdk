using System;
using System.Threading.Tasks;
using McpSdk.Protocol;

namespace McpSdk.Client
{
    public interface IRootsCapability
    {
        event Action ListChanged;
        bool IsListChangedNotificationSupported { get; }
        Task<ListRootsResult> ListRoots();
    }
}