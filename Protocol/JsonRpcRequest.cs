namespace McpSharp.Protocol
{
    public sealed class JsonRpcRequest<TId, TParams>
    {
        public string JsonRpcVersion { get; }
        public TId Id { get; }
        public string Method { get; }
        public TParams Parameters { get; }

        public JsonRpcRequest(TId id, string method, TParams parameters)
        {
            JsonRpcVersion = "2.0";
            Id = id;
            Method = method;
            Parameters = parameters;
        }
    }
}