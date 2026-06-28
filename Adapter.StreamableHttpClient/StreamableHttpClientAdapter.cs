using System;
using System.IO;
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

        public async Task OpenStream(
            string sessionId,
            string protocolVersion,
            string lastEventId,
            Action<string, string> onEvent,
            CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _endpointUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            if (!string.IsNullOrEmpty(sessionId))
                request.Headers.TryAddWithoutValidation(SessionIdHeader, sessionId);
            if (!string.IsNullOrEmpty(protocolVersion))
                request.Headers.TryAddWithoutValidation(ProtocolVersionHeader, protocolVersion);
            if (!string.IsNullOrEmpty(lastEventId))
                request.Headers.TryAddWithoutValidation("Last-Event-ID", lastEventId);

            try
            {
                using var httpResponse = await _httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    // A server that does not offer the standalone stream (e.g. 405) is fine — the client
                    // simply never receives server-initiated traffic. Nothing to pump.
                    return;
                }

                using var stream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                // Minimal SSE framing: accumulate `data:` lines and the event `id:`, dispatch on the
                // blank line that terminates each event. `event:` is unused (all events are messages).
                var data = new StringBuilder();
                string eventId = null;
                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line == null)
                        break; // stream closed by the server

                    if (line.Length == 0)
                    {
                        if (data.Length > 0)
                        {
                            var payload = data.ToString();
                            data.Clear();
                            onEvent(eventId, payload);
                        }
                        eventId = null;
                        continue;
                    }

                    if (line[0] == ':')
                        continue; // comment / heartbeat

                    if (line.StartsWith("data:", StringComparison.Ordinal))
                    {
                        var value = line.Substring(5).TrimStart(' ');
                        if (data.Length > 0)
                            data.Append('\n');
                        data.Append(value);
                    }
                    else if (line.StartsWith("id:", StringComparison.Ordinal))
                    {
                        eventId = line.Substring(3).TrimStart(' ');
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Server→client stream ended: {ex.Message}");
            }
        }

        public async Task DeleteSession(string sessionId, string protocolVersion, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, _endpointUrl);
            if (!string.IsNullOrEmpty(sessionId))
                request.Headers.TryAddWithoutValidation(SessionIdHeader, sessionId);
            if (!string.IsNullOrEmpty(protocolVersion))
                request.Headers.TryAddWithoutValidation(ProtocolVersionHeader, protocolVersion);

            try
            {
                using var _ = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"DELETE session failed: {ex.Message}");
            }
        }

        public void Dispose() => _httpClient.Dispose();
    }
}
