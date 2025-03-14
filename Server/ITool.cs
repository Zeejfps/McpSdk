using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server;

public interface ITool
{
    Tool Info { get; }
    Task<CallToolResult> Call(IJsonObject arguments);
}