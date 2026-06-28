using McpSdk.Protocol;

namespace McpSdk.Shared
{
    /// <summary>
    /// An encoded, outbound JSON-RPC 2.0 frame: the serialized wire <see cref="Payload"/> bundled with the
    /// routing metadata the producer already knew at encode time — its <see cref="Kind"/> and, for a
    /// request or response, its <see cref="Id"/>. Carrying that metadata alongside the text lets a channel
    /// route the frame — a response back onto the POST that carried its request, a request/notification
    /// onto the SSE stream — <em>without</em> re-parsing the JSON the codec just serialized.
    ///
    /// This is the encode-side mirror of <see cref="JsonRpcMessage"/> (the decode-side classification of
    /// an inbound frame): outbound the producer knows the structure, so it travels with the bytes; inbound
    /// the receiver only has bytes off the wire and must parse. It is a <c>readonly struct</c>, so it adds
    /// no allocation over passing the bare string.
    /// </summary>
    public readonly struct JsonRpcFrame
    {
        private JsonRpcFrame(JsonRpcMessageKind kind, RequestId id, string payload)
        {
            Kind = kind;
            Id = id;
            Payload = payload;
        }

        public JsonRpcMessageKind Kind { get; }

        /// <summary>The id. Meaningful for <see cref="JsonRpcMessageKind.Request"/> and <see cref="JsonRpcMessageKind.Response"/>.</summary>
        public RequestId Id { get; }

        /// <summary>The serialized JSON-RPC 2.0 text to put on the wire.</summary>
        public string Payload { get; }

        public static JsonRpcFrame Request(RequestId id, string payload) =>
            new JsonRpcFrame(JsonRpcMessageKind.Request, id, payload);

        public static JsonRpcFrame Notification(string payload) =>
            new JsonRpcFrame(JsonRpcMessageKind.Notification, default, payload);

        public static JsonRpcFrame Response(RequestId id, string payload) =>
            new JsonRpcFrame(JsonRpcMessageKind.Response, id, payload);
    }
}
