namespace McpSharp.Protocol
{
    public abstract class JsonRpcRequest<TId>
    {
        public string JsonRpcVersion { get; }
        public TId Id { get; }
        public string Method { get; }

        protected JsonRpcRequest(TId id, string method)
        {
            JsonRpcVersion = "2.0";
            Id = id;
            Method = method;
        }
    }
    
    public sealed class JsonRpcRequest<TId, TParams> : JsonRpcRequest<TId>
    {
        public TParams Parameters { get; }

        public JsonRpcRequest(TId id, string method, TParams parameters) : base(id, method)
        {
            Parameters = parameters;
        }
    }
}