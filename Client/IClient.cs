using System.Collections.Generic;
using System.Threading.Tasks;

namespace McpSharp.Client
{
    public interface IClient
    {
        bool IsConnected { get; }
        Task Connect();
        Task<IEnumerable<IToolInfo>> ListTools();
        Task<ICallToolResult> CallTool(string toolName);
    }
}