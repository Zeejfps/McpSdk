using McpSharp.Protocol;
using McpSharp.Protocol.Messages;

namespace McpSharp.Client
{
    public interface IJson
    {
        string Stringify(JsonRpcRequest<int,InitializeRequestPayload> jsonRpcRequest);
        string Stringify(JsonRpcRequest<int,ListToolsRequestPayload> jsonRpcRequest);
        string Stringify(JsonRpcRequest<int,CallToolRequestPayload> jsonRpcRequest);
        string Stringify(JsonRpcNotification jsonRpcNotification);
        void Parse(string jsonString, out JsonRpcResponse<int, InitializeResultPayload> jsonRpcResponse);
        void Parse(string jsonString, out JsonRpcResponse<int, ListToolsResultPayload> jsonRpcResponse);
        void Parse(string jsonString, out JsonRpcResponse<int, CallToolResultPayload> jsonRpcResponse);
    }
}