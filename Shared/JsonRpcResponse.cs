using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Shared
{
    /// <summary>
    /// A JSON-RPC response: an id paired with either a <c>result</c> (success) or an <c>error</c>. The
    /// outbound forms are built through <see cref="Result"/> / <see cref="Failure"/>; the inbound form is
    /// decoded from the wire and split into an <see cref="IResponse"/> by <see cref="ToResponse"/>.
    /// </summary>
    public sealed class JsonRpcResponse : JsonRpcMessage
    {
        private readonly Json _writeResult;
        private readonly Error _error;
        private readonly IJsonObject _raw;

        private JsonRpcResponse(RequestId id, Json writeResult, Error error, IJsonObject raw)
        {
            Id = id;
            _writeResult = writeResult;
            _error = error;
            _raw = raw;
        }

        /// <summary>Outbound success: <c>{ id, result }</c>.</summary>
        public static JsonRpcResponse Result(RequestId id, Json result) =>
            new JsonRpcResponse(id, result, null, null);

        /// <summary>Outbound failure: <c>{ id, error }</c>.</summary>
        public static JsonRpcResponse Failure(RequestId id, Error error) =>
            new JsonRpcResponse(id, null, error, null);

        /// <summary>Inbound: a response decoded from the wire (carries the full parsed object).</summary>
        public JsonRpcResponse(RequestId id, IJsonObject raw) : this(id, null, null, raw)
        {
        }

        public RequestId Id { get; }

        /// <summary>Splits a decoded response into result-vs-error. Meaningful for the inbound form.</summary>
        public IResponse ToResponse()
        {
            var errorProperty = _raw["error"];
            return errorProperty == null
                ? Response.FromResult(_raw["result"].AsObject())
                : Response.FromError(new Error(errorProperty.AsObject()));
        }

        protected override void WriteBody(IJsonWriter writer)
        {
            Id.WriteTo(writer, "id");
            if (_error != null)
                _error.WriteTo(writer, "error");
            else if (_writeResult != null)
                _writeResult.WriteTo(writer, "result");
            else if (_raw != null)
            {
                // A decoded response being re-serialized: copy across whichever body it carried.
                var errorProperty = _raw["error"];
                if (errorProperty != null)
                    errorProperty.WriteTo(writer, "error");
                else
                    _raw["result"].WriteTo(writer, "result");
            }
        }
    }
}
