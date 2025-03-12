using System;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Client
{
    public interface IClient
    {
        bool IsConnected { get; }
        Task Connect();
        Task<ListToolsResult> ListTools();
        Task<CallToolResult> CallTool(string toolName, Action<IJsonWriter> args);
    }
}