using System.Threading.Tasks;
using McpSdk.Protocol;

namespace McpSdk.Server
{
    public interface IToolsCapability
    {
        bool IsListChangedNotificationSupported { get; }
        Task<ListToolsResult> ListTools();
        Task<CallToolResult> CallTool(CallToolArguments arguments);
    }
}