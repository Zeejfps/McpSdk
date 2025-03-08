using System.Collections.Generic;
using System.Threading.Tasks;
using McpSharp.Protocol;

namespace McpSharp.Client
{
    public interface IClient
    {
        bool IsConnected { get; }
        Task Connect();
        Task<IEnumerable<Tool>> ListTools();
        Task<ICallToolResult> CallTool(string toolName);
    }
}