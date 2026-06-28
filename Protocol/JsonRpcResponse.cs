using System;
using McpSdk.Protocol.Models;

namespace McpSdk.Protocol
{
    /// <summary>
    /// A JSON-RPC response: an id paired with either a <c>result</c> (success) or an <c>error</c>. The
    /// outbound forms are built through <see cref="Ok"/> / <see cref="Failure"/>; the inbound form is
    /// decoded from the wire (carrying the full parsed object) and read through <see cref="IsOk"/> /
    /// <see cref="Result"/> / <see cref="Error"/> / <see cref="Unwrap{T}"/>.
    ///
    /// One type spans both directions: the reply to an <see cref="ITransport.SendRequest"/> is read off it,
    /// and the argument to <see cref="ITransport.SendResponse"/> is built on it — so there is no separate
    /// transport-neutral response model to translate to and from.
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

        /// <summary>Outbound success: <c>{ id, result }</c>, the result supplied by a model's <c>WriteMembers</c>.</summary>
        public static JsonRpcResponse Ok(RequestId id, Json result) =>
            new JsonRpcResponse(id, result, null, null);

        /// <summary>Outbound failure: <c>{ id, error }</c>.</summary>
        public static JsonRpcResponse Failure(RequestId id, Error error) =>
            new JsonRpcResponse(id, null, error, null);

        /// <summary>Inbound: a response decoded from the wire (carries the full parsed object).</summary>
        public JsonRpcResponse(RequestId id, IJsonObject raw) : this(id, null, null, raw)
        {
        }

        public RequestId Id { get; }

        /// <summary>True when this response carries a result rather than an error.</summary>
        public bool IsOk => !IsError;

        /// <summary>True when this response carries an error.</summary>
        public bool IsError => _error != null || (_raw != null && _raw["error"] != null);

        /// <summary>The decoded result body (inbound only). <c>null</c> on the error branch or an outbound response.</summary>
        public IJsonObject Result => _raw?["result"]?.AsObject();

        /// <summary>The error (an outbound failure, or an inbound error response). <c>null</c> on the success branch.</summary>
        public Error Error
        {
            get
            {
                if (_error != null)
                    return _error;
                var errorProperty = _raw?["error"];
                return errorProperty == null ? null : new Error(errorProperty.AsObject());
            }
        }

        /// <summary>Collapses the result-vs-error split into a single value (inbound consumption).</summary>
        public T Unwrap<T>(Func<IJsonObject, T> onResult, Func<Error, T> onError)
        {
            return IsError ? onError(Error) : onResult(Result);
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
