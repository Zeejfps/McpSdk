using McpSdk.Protocol;

namespace McpSdk.Shared
{
    public enum JsonRpcMessageKind
    {
        Request,
        Notification,
        Response,
    }

    /// <summary>
    /// A decoded, classified JSON-RPC 2.0 frame. The product of <see cref="JsonRpcCodec.TryDecode(string, out JsonRpcMessage)"/>,
    /// so every transport classifies inbound messages the same way regardless of how they were framed
    /// (newline-delimited stdio, an HTTP POST body, or an SSE <c>data:</c> event).
    /// </summary>
    public readonly struct JsonRpcMessage
    {
        private JsonRpcMessage(JsonRpcMessageKind kind, RequestId id, string method, IJsonObject parameters, IJsonObject raw)
        {
            Kind = kind;
            Id = id;
            Method = method;
            Parameters = parameters;
            Raw = raw;
        }

        public JsonRpcMessageKind Kind { get; }

        /// <summary>The request id. Meaningful for <see cref="JsonRpcMessageKind.Request"/> and <see cref="JsonRpcMessageKind.Response"/>.</summary>
        public RequestId Id { get; }

        /// <summary>The method. Meaningful for <see cref="JsonRpcMessageKind.Request"/> and <see cref="JsonRpcMessageKind.Notification"/>.</summary>
        public string Method { get; }

        /// <summary>The <c>params</c> object (may be null). Meaningful for requests and notifications.</summary>
        public IJsonObject Parameters { get; }

        /// <summary>The full parsed object — used to read a response's <c>result</c>/<c>error</c>.</summary>
        public IJsonObject Raw { get; }

        public static JsonRpcMessage Request(RequestId id, string method, IJsonObject parameters, IJsonObject raw) =>
            new JsonRpcMessage(JsonRpcMessageKind.Request, id, method, parameters, raw);

        public static JsonRpcMessage Notification(string method, IJsonObject parameters, IJsonObject raw) =>
            new JsonRpcMessage(JsonRpcMessageKind.Notification, default, method, parameters, raw);

        public static JsonRpcMessage Response(RequestId id, IJsonObject raw) =>
            new JsonRpcMessage(JsonRpcMessageKind.Response, id, null, null, raw);
    }
}
