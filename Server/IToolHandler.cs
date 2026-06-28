using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server;

public interface IToolHandler
{
    Tool Tool { get; }
    Task<CallToolResult> Call(IJsonObject arguments, McpRequestContext context);
}