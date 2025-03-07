namespace McpSharp.Protocol
{
    public sealed class JsonRpcResponse<TId, TResult> where TResult : class
    {
        public string JsonRpcVersion { get; }
        public TId Id { get; }
        public TResult Result { get; }
        public JsonRpcResponseError Error { get; }

        public JsonRpcResponse(string jsonRpcVersion, TId id, TResult result, JsonRpcResponseError error)
        {
            JsonRpcVersion = jsonRpcVersion;
            Id = id;
            Result = result;
            Error = error;
        }
    }
    
    public sealed class JsonRpcResponseError
    {
        public int Code { get; }
        public string Message { get; }
        public object Data { get; }

        public JsonRpcResponseError(int code, string message, object data)
        {
            Code = code;
            Message = message;
            Data = data;
        }
    }
}