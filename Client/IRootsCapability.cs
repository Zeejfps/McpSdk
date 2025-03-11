using System;
using System.Threading.Tasks;
using McpSharp.Protocol;

namespace McpSharp.Client
{
    public interface IRootsCapability
    {
        event Action ListChanged;
        bool IsListChangedNotificationSupported { get; }
        Task<ListRootsResult> ListRoots();
    }
}