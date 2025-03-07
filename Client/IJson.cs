using McpSharp.Protocol;
using McpSharp.Protocol.Messages;

namespace McpSharp.Client
{
    public interface IJson
    {
        string Stringify(JsonRpcRequest<int, InitializeMessage> request);
        string Stringify(JsonRpcNotification request);
    }
}