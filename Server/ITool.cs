using System.Threading.Tasks;
using McpSharp.Protocol;

namespace McpSharp.Server
{
    public interface ITool
    {
        Task<CallToolResult> Call();
    }
}