using System;
using System.Threading;
using System.Threading.Tasks;

namespace McpSdk.Shared
{
    /// <summary>
    /// A dumb, duplex pipe for already-encoded JSON-RPC frames. It carries frames in both directions and
    /// manages its own connection lifecycle — and knows nothing about requests, responses, ids, or
    /// correlation. That all lives one layer up in <see cref="JsonRpcPeer"/>.
    ///
    /// Note the deliberate asymmetry between the two directions. Outbound, the peer hands down a
    /// <see cref="JsonRpcFrame"/> that already carries the kind/id it knew at encode time, so a channel can
    /// route (e.g. response→originating POST, request/notification→SSE) without re-parsing JSON. Inbound,
    /// the channel only has raw bytes off the wire, so it raises an opaque <c>string</c> and the peer
    /// parses it. Producer knows the structure; receiver must recover it.
    ///
    /// This is the seam where transports genuinely differ: a stdio channel is a line reader/writer; an
    /// HTTP channel is POST/SSE + sessions. Everything above this interface — correlation, dispatch, the
    /// MCP protocol — is shared across every transport.
    /// </summary>
    public interface IMessageChannel
    {
        /// <summary>Raised for each inbound frame (one JSON-RPC message), as raw wire text.</summary>
        event Action<string> FrameReceived;

        Task Start(CancellationToken cancellationToken = default);

        Task Stop();

        /// <summary>
        /// Sends one outbound frame. A channel may read <see cref="JsonRpcFrame.Kind"/>/<see cref="JsonRpcFrame.Id"/>
        /// to route it (e.g. an HTTP channel sends a response back on the originating POST), but it never
        /// correlates or awaits a reply — that is the peer's job.
        /// </summary>
        Task Send(JsonRpcFrame frame, CancellationToken cancellationToken = default);
    }
}
