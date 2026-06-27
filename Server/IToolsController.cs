using System;
using System.Threading.Tasks;
using McpSdk.Protocol.Models;

namespace McpSdk.Server
{
    public interface IToolsController
    {
        event Action ListChanged;
        bool IsListChangedNotificationSupported { get; }
        Task<ListToolsResult> ListTools(ListToolsRequest request);
        Task<CallToolResult> CallTool(CallToolRequest request);
    }
}