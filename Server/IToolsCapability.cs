using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server
{
    public interface IToolsCapability
    {
        bool IsListChangedNotificationSupported { get; }
        Task<ListToolsResult> ListTools();
        Task<CallToolResult> CallTool(CallToolArguments arguments);
    }
}