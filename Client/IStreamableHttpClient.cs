using System;
using System.Threading;
using System.Threading.Tasks;

namespace McpSdk.Client
{
    /// <summary>
    /// The HTTP reply to a single Streamable HTTP POST, reduced to what the transport needs: the status
    /// code, the response <c>Content-Type</c>, the <c>application/json</c> body (when present), and the
    /// <c>Mcp-Session-Id</c> the server issued on initialize.
    /// </summary>
    public sealed class StreamableHttpResponse
    {
        public int StatusCode { get; set; }
        public string ContentType { get; set; }
        public string Body { get; set; }
        public string SessionId { get; set; }
    }

    /// <summary>
    /// Abstraction over the concrete HTTP client (a <c>System.Net.Http.HttpClient</c> lives in the
    /// adapter), mirroring the old <c>ISseClient</c> split so the core <c>Client</c> library stays free
    /// of a hard HTTP dependency. <paramref name="sessionId"/> and <paramref name="protocolVersion"/>
    /// are <c>null</c> before initialization and become the <c>Mcp-Session-Id</c> /
    /// <c>MCP-Protocol-Version</c> request headers once known.
    /// </summary>
    public interface IStreamableHttpClient
    {
        Task<StreamableHttpResponse> PostMessage(
            string jsonBody,
            string sessionId,
            string protocolVersion,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens the standalone server→client SSE stream (an HTTP <c>GET</c> to the endpoint), resuming
        /// after <paramref name="lastEventId"/> when supplied, and pumps each event's
        /// <c>(eventId, json)</c> to <paramref name="onEvent"/> (<c>eventId</c> may be <c>null</c>). The
        /// returned task completes when the stream closes or <paramref name="cancellationToken"/> fires.
        /// </summary>
        Task OpenStream(
            string sessionId,
            string protocolVersion,
            string lastEventId,
            Action<string, string> onEvent,
            CancellationToken cancellationToken = default);

        /// <summary>Terminates the session with an HTTP <c>DELETE</c> (best effort).</summary>
        Task DeleteSession(
            string sessionId,
            string protocolVersion,
            CancellationToken cancellationToken = default);
    }
}
