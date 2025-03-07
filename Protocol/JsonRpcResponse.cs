namespace McpSharp.Protocol
{
    public sealed class JsonRpcResponse<T> where T : class
    {
        public string JsonRpcVersion { get; }
        public object Id { get; }
        public T Result { get; }
        public JsonRpcResponseError Error { get; }
    }
    
    public sealed class JsonRpcResponseError
    {
        public int Code { get; }
        public string Message { get; }
        public object Data { get; }
    }
}