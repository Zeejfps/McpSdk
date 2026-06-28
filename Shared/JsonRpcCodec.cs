using System;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Shared
{
    /// <summary>
    /// The single source of truth for JSON-RPC 2.0 envelope construction and decoding. Every transport —
    /// <see cref="JsonRpcTransport"/> (stdio) and the Streamable HTTP client/server transports — encodes
    /// and classifies messages through this codec, so the wire shapes live in exactly one place. The
    /// codec is stateless and framing-agnostic: it neither sends bytes nor correlates ids, so it composes
    /// with both the symmetric single-channel model and HTTP's request-coupled, per-connection routing.
    /// </summary>
    public sealed class JsonRpcCodec
    {
        public const string JsonRpcVersion = "2.0";

        private readonly IJson _json;

        public JsonRpcCodec(IJson json)
        {
            _json = json ?? throw new ArgumentNullException(nameof(json));
        }

        public string EncodeRequest(RequestId id, string method, Json parameters) => _json.Stringify(writer =>
        {
            JsonRpcVersion.WriteTo(writer, "jsonrpc");
            id.WriteTo(writer, "id");
            method.WriteTo(writer, "method");
            parameters.WriteTo(writer, "params");
        });

        public string EncodeNotification(string method, Json parameters) => _json.Stringify(writer =>
        {
            JsonRpcVersion.WriteTo(writer, "jsonrpc");
            method.WriteTo(writer, "method");
            if (parameters != null)
                parameters.WriteTo(writer, "params");
        });

        public string EncodeResult(RequestId id, Json result) => _json.Stringify(writer =>
        {
            JsonRpcVersion.WriteTo(writer, "jsonrpc");
            id.WriteTo(writer, "id");
            result.WriteTo(writer, "result");
        });

        public string EncodeError(RequestId id, Error error) => _json.Stringify(writer =>
        {
            JsonRpcVersion.WriteTo(writer, "jsonrpc");
            id.WriteTo(writer, "id");
            error.WriteTo(writer, "error");
        });

        /// <summary>Turns a JSON-RPC response object into an <see cref="IResponse"/> (result vs error).</summary>
        public IResponse ParseResponse(IJsonObject responseObject)
        {
            var errorProperty = responseObject["error"];
            if (errorProperty == null)
                return Response.FromResult(responseObject["result"].AsObject());
            return Response.FromError(new Error(errorProperty.AsObject()));
        }

        /// <summary>
        /// Parses and classifies a raw inbound frame. Returns <c>false</c> for a JSON-RPC batch (removed
        /// in 2025-06-18), a parse failure, or a frame that is neither a request/notification nor a
        /// response — the caller decides how to log/ignore those.
        /// </summary>
        public bool TryDecode(string messageJson, out JsonRpcMessage message)
        {
            message = default;

            if (JsonRpcFraming.IsBatch(messageJson))
                return false;

            IJsonObject obj;
            try
            {
                obj = _json.Parse(messageJson);
            }
            catch
            {
                return false;
            }

            return TryDecode(obj, out message);
        }

        /// <summary>Classifies an already-parsed JSON-RPC object. See <see cref="TryDecode(string, out JsonRpcMessage)"/>.</summary>
        public bool TryDecode(IJsonObject obj, out JsonRpcMessage message)
        {
            message = default;
            if (obj == null)
                return false;

            var idProperty = obj["id"];
            var method = obj["method"]?.AsString();

            if (method != null)
            {
                message = idProperty == null
                    ? JsonRpcMessage.Notification(method, obj["params"]?.AsObject(), obj)
                    : JsonRpcMessage.Request(RequestId.FromJson(idProperty), method, obj["params"]?.AsObject(), obj);
                return true;
            }

            if (idProperty != null)
            {
                message = JsonRpcMessage.Response(RequestId.FromJson(idProperty), obj);
                return true;
            }

            return false; // neither a method nor an id — not a dispatchable JSON-RPC message
        }
    }
}
