using System;
using System.Threading.Tasks;
using McpSharp.Protocol;

namespace McpSharp.Client
{
    public interface IClient
    {
        bool IsConnected { get; }
        Task Connect();
        Task<ListToolsResult> ListTools();
        Task<IJsonObject> CallTool(string toolName, Action<IJsonWriter> args);
    }
}