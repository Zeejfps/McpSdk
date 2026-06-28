namespace McpSdk.Protocol
{
    /// <summary>A JSON-RPC request: an id-bearing method call that expects a response.</summary>
    public sealed class JsonRpcRequest : JsonRpcMessage
    {
        private readonly Json _writeParameters;

        /// <summary>Outbound: a request to send, its <c>params</c> supplied as a writer.</summary>
        public JsonRpcRequest(RequestId id, string method, Json parameters)
        {
            Id = id;
            Method = method;
            _writeParameters = parameters;
        }

        /// <summary>Inbound: a request decoded from the wire, its <c>params</c> exposed for dispatch.</summary>
        public JsonRpcRequest(RequestId id, string method, IJsonObject parameters)
        {
            Id = id;
            Method = method;
            Parameters = parameters;
            _writeParameters = parameters == null ? null : parameters.WriteMembers;
        }

        public RequestId Id { get; }

        public string Method { get; }

        /// <summary>The decoded <c>params</c> object (inbound only; <c>null</c> on an outbound request).</summary>
        public IJsonObject Parameters { get; }

        protected override void WriteBody(IJsonWriter writer)
        {
            Id.WriteTo(writer, "id");
            Method.WriteTo(writer, "method");
            _writeParameters.WriteTo(writer, "params");
        }
    }
}
