using System;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;

namespace McpSdk.Shared
{
    /// <summary>
    /// The wire boundary for one connection: it converts between <see cref="JsonRpcMessage"/> models and
    /// the bytes a given transport puts on the wire, and manages its own connection lifecycle. It knows
    /// nothing about request/response correlation or id generation — that lives one layer up in
    /// <see cref="JsonRpcPeer"/>, which works purely in messages.
    ///
    /// Both directions speak <see cref="JsonRpcMessage"/>: <see cref="Send"/> renders an outbound message
    /// to the wire (and, where a transport requires it, routes it — e.g. an HTTP channel returns a
    /// response on the POST that carried its request), and <see cref="MessageReceived"/> surfaces an
    /// inbound message parsed off the wire. This is the seam where transports genuinely differ: a stdio
    /// channel is a line reader/writer; an HTTP channel is POST/SSE + sessions. Everything above this
    /// interface — correlation, dispatch, the MCP protocol — is shared across every transport.
    /// </summary>
    public interface IMessageChannel
    {
        /// <summary>Raised for each inbound message parsed off the wire.</summary>
        event Action<JsonRpcMessage> MessageReceived;

        Task Start(CancellationToken cancellationToken = default);

        Task Stop();

        /// <summary>
        /// Renders and sends one outbound message. A channel may inspect the message to route it (e.g. an
        /// HTTP channel returns a <see cref="JsonRpcResponse"/> on the originating POST), but it never
        /// correlates or awaits a reply — that is the peer's job.
        /// </summary>
        Task Send(JsonRpcMessage message, CancellationToken cancellationToken = default);
    }
}
