using System.Threading.Tasks;
using McpSdk.Protocol;

namespace McpSdk.Server
{
    public interface ITool
    {
        Task<CallToolResult> Call();
    }
}