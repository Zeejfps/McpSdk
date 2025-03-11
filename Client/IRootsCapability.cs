using System;
using System.Threading.Tasks;

namespace McpSharp.Client
{
    public interface IRoot
    {
        string Uri { get; }
        string Name { get; }
    }
    
    public interface IListRootsResult
    {
        IRoot[] Roots { get; }
    }
    
    public interface IRootsCapability
    {
        event Action ListChanged;
        bool IsListChangedNotificationSupported { get; }
        Task<IListRootsResult> ListRoots();
    }
}