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
    }
}
