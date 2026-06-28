using System;
using System.Threading;
using System.Threading.Tasks;

namespace McpSdk.Shared
{
    /// <summary>
    /// A dumb, duplex pipe for already-encoded JSON-RPC frames. It carries opaque string frames in both
    /// directions and manages its own connection lifecycle — and knows nothing about requests,
    /// responses, ids, or correlation. That all lives one layer up in <see cref="JsonRpcPeer"/>.
    ///
    /// This is the seam where transports genuinely differ: a stdio channel is a line reader/writer; an
    /// HTTP channel is POST/SSE + sessions. Everything above this interface — correlation, dispatch, the
    /// MCP protocol — is shared across every transport.
    /// </summary>
    public interface IMessageChannel
    {
        /// <summary>Raised for each inbound frame (one JSON-RPC message).</summary>
        event Action<string> FrameReceived;

        Task Start(CancellationToken cancellationToken = default);

        Task Stop();

        /// <summary>
        /// Sends one outbound frame. A channel may inspect the frame to route it (e.g. an HTTP channel
        /// peeks the id to send a response back on the originating POST), but it never correlates or
        /// awaits a reply — that is the peer's job.
        /// </summary>
        Task Send(string frame, CancellationToken cancellationToken = default);
    }
}
