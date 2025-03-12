using System;
using System.Threading.Tasks;
using McpSdk.Protocol.Models;

namespace McpSdk.Server
{
    public interface IToolsController
    {
        event Action ListChanged;
        bool IsListChangedNotificationSupported { get; }
        Task<ListToolsResult> ListTools();
        Task<CallToolResult> CallTool(CallToolArguments arguments);
    }
}