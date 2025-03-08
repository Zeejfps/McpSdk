using McpSharp.Protocol;
using McpSharp.Protocol.Messages;

namespace McpSharp.Client
{
    public interface IJson
    {
        string Stringify(JsonRpcRequest<int, InitializeRequestPayload> jsonRpcRequest);
        string Stringify(JsonRpcNotification jsonRpcNotification);
        string Stringify(JsonRpcRequest<int, ListToolsRequestPayload> jsonRpcRequest);
        void Parse(string jsonString, out JsonRpcResponse<int, InitializeResultPayload> jsonRpcResponse);
        void Parse(string jsonString, out JsonRpcResponse<int, ListToolsResultPayload> jsonRpcResponse);
    }
}