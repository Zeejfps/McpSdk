using System.Threading.Tasks;
using McpSdk.Protocol;

namespace McpSdk.Server
{
    public interface IToolsCapability
    {
        Task<ListToolsResult> ListTools();
        Task<CallToolResult> CallTool(string toolName);
    }
}