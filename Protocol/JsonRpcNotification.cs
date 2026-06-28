namespace McpSdk.Protocol
{
    /// <summary>A JSON-RPC notification: a method call with no id, so it expects no response.</summary>
    public sealed class JsonRpcNotification : JsonRpcMessage
    {
        private readonly Json _writeParameters;

        /// <summary>Outbound: a notification to send, its <c>params</c> supplied as a writer.</summary>
        public JsonRpcNotification(string method, Json parameters)
        {
            Method = method;
            _writeParameters = parameters;
        }

        /// <summary>Inbound: a notification decoded from the wire, its <c>params</c> exposed for dispatch.</summary>
        public JsonRpcNotification(string method, IJsonObject parameters)
        {
            Method = method;
            Parameters = parameters;
            _writeParameters = parameters == null ? null : parameters.WriteMembers;
        }

        public string Method { get; }

        /// <summary>The decoded <c>params</c> object (inbound only; may be <c>null</c>).</summary>
        public IJsonObject Parameters { get; }

        protected override void WriteBody(IJsonWriter writer)
        {
            Method.WriteTo(writer, "method");
            if (_writeParameters != null)
                _writeParameters.WriteTo(writer, "params");
        }
    }
}
