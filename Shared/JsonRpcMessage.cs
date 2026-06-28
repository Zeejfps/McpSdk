using McpSdk.Protocol;

namespace McpSdk.Shared
{
    /// <summary>
    /// A JSON-RPC 2.0 message, modeled the same way as <see cref="McpSdk.Protocol.Models.Content"/>: an
    /// abstract base with one concrete subtype per wire shape — <see cref="JsonRpcRequest"/>,
    /// <see cref="JsonRpcNotification"/>, <see cref="JsonRpcResponse"/> — each of which both writes itself
    /// (<see cref="WriteMembers"/>) and is reconstructed from a parsed object (<see cref="FromJsonObject"/>).
    /// One type per kind owns both directions, so the wire shape for a kind lives in exactly one place
    /// (rather than being split across a separate encoder and decoder).
    ///
    /// The base contributes the shared envelope: <see cref="WriteMembers"/> emits the
    /// <c>"jsonrpc":"2.0"</c> header and defers the rest to <see cref="WriteBody"/>. Because a message both
    /// encodes and decodes, the same model flows in both directions across an <see cref="IMessageChannel"/>
    /// — there is no separate "frame to send" vs "message to dispatch".
    /// </summary>
    public abstract class JsonRpcMessage : IJsonObjectWriter
    {
        public const string JsonRpcVersion = "2.0";

        public void WriteMembers(IJsonWriter writer)
        {
            JsonRpcVersion.WriteTo(writer, "jsonrpc");
            WriteBody(writer);
        }

        /// <summary>Writes the kind-specific members (id/method/params/result/error) after the shared header.</summary>
        protected abstract void WriteBody(IJsonWriter writer);

        /// <summary>
        /// Classifies a parsed JSON-RPC object into its concrete subtype, mirroring
        /// <see cref="McpSdk.Protocol.Models.Content.FromJsonObject"/>. Returns <c>null</c> for an object
        /// that is neither a request/notification nor a response (no <c>method</c> and no <c>id</c>).
        /// </summary>
        public static JsonRpcMessage FromJsonObject(IJsonObject obj)
        {
            if (obj == null)
                return null;

            var idProperty = obj["id"];
            var method = obj["method"]?.AsString();

            if (method != null)
                return idProperty == null
                    ? (JsonRpcMessage)new JsonRpcNotification(method, obj["params"]?.AsObject())
                    : new JsonRpcRequest(RequestId.FromJson(idProperty), method, obj["params"]?.AsObject());

            if (idProperty != null)
                return new JsonRpcResponse(RequestId.FromJson(idProperty), obj);

            return null; // neither a method nor an id — not a dispatchable JSON-RPC message
        }

        /// <summary>
        /// Parses a raw inbound frame and classifies it. Returns <c>false</c> for a JSON-RPC batch (removed
        /// in 2025-06-18), a parse failure, or a non-dispatchable object — the channel decides how to
        /// log/ignore those.
        /// </summary>
        public static bool TryParse(IJson json, string frame, out JsonRpcMessage message)
        {
            message = null;
            if (string.IsNullOrEmpty(frame) || JsonRpcFraming.IsBatch(frame))
                return false;

            IJsonObject obj;
            try
            {
                obj = json.Parse(frame);
            }
            catch
            {
                return false;
            }

            message = FromJsonObject(obj);
            return message != null;
        }
    }
}
