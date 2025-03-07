using McpSharp.Protocol;
using McpSharp.Protocol.Messages;

namespace McpSharp.Client
{
    public interface IJson
    {
        string Stringify(JsonRpcRequest<int, InitializeMessage> jsonRpcRequest);
        string Stringify(JsonRpcNotification jsonRpcNotification);
        void Parse(string jsonString, out JsonRpcResponse<int, InitializeResponseMessage> jsonRpcResponse);
    }
}