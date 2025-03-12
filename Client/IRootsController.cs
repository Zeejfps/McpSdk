using System;
using System.Threading.Tasks;
using McpSdk.Protocol.Models;

namespace McpSdk.Client
{
    public interface IRootsController
    {
        event Action ListChanged;
        bool IsListChangedNotificationSupported { get; }
        Task<ListRootsResult> ListRoots();
    }
}