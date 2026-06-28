using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Client;
using McpSdk.Shared;

namespace McpSdk.Adapter.StreamableHttpClient
{
    /// <summary>
    /// <see cref="System.Net.Http.HttpClient"/>-backed <see cref="IStreamableHttpClient"/>: POSTs a
    /// JSON-RPC frame to the single MCP endpoint, advertising it accepts both <c>application/json</c> and
    /// <c>text/event-stream</c>, and forwarding the <c>Mcp-Session-Id</c> / <c>MCP-Protocol-Version</c>
    /// headers once the transport knows them. Reads back the <c>application/json</c> body and the
    /// <c>Mcp-Session-Id</c> the server issues on initialize.
    /// </summary>
    public sealed class StreamableHttpClientAdapter : IStreamableHttpClient, IDisposable
    {
        private const string SessionIdHeader = "Mcp-Session-Id";
        private const string ProtocolVersionHeader = "MCP-Protocol-Version";

        private readonly HttpClient _httpClient;
        private readonly string _endpointUrl;
        private readonly ILogger _logger;

        public StreamableHttpClientAdapter(string endpointUrl, ILoggerFactory loggerFactory)
        {
            _endpointUrl = endpointUrl;
            _logger = loggerFactory.Create<StreamableHttpClientAdapter>();
            _httpClient = new HttpClient();
        }

        public async Task<StreamableHttpResponse> PostMessage(
            string jsonBody,
            string sessionId,
            string protocolVersion,
            CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpointUrl)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json"),
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            if (!string.IsNullOrEmpty(sessionId))
                request.Headers.TryAddWithoutValidation(SessionIdHeader, sessionId);
            if (!string.IsNullOrEmpty(protocolVersion))
                request.Headers.TryAddWithoutValidation(ProtocolVersionHeader, protocolVersion);

            using var httpResponse = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);

            var result = new StreamableHttpResponse
            {
                StatusCode = (int)httpResponse.StatusCode,
                ContentType = httpResponse.Content?.Headers?.ContentType?.MediaType,
            };

            if (httpResponse.Headers.TryGetValues(SessionIdHeader, out var ids))
            {
                foreach (var id in ids)
                {
                    result.SessionId = id;
                    break;
                }
            }

            if (httpResponse.Content != null &&
                result.ContentType != null &&
                result.ContentType.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result.Body = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            return result;
        }

        public void Dispose() => _httpClient.Dispose();
    }
}
