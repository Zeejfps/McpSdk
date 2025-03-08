using System.Collections.Generic;
using System.Threading.Tasks;
using McpSharp.Protocol;
using McpSharp.Protocol.Messages;

namespace McpSharp.Client
{
    public interface IClient
    {
        bool IsConnected { get; }
        Task Connect();
        Task<IEnumerable<ToolInfo>> ListTools();
        Task<ICallToolResult> CallTool(string toolName);
    }
}