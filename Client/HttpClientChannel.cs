using System;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Client
{
    /// <summary>
    /// The Streamable HTTP client channel: the HTTP/SSE delivery layer, exposed as a dumb
    /// <see cref="IMessageChannel"/> so a shared <see cref="JsonRpcPeer"/> sits on top. Outbound frames
    /// are POSTed; because HTTP couples a request's response to its POST, a request's reply body is fed
    /// straight back through <see cref="FrameReceived"/> for the peer to correlate — race-free, because
    /// the peer registers the pending reply before calling <see cref="Send"/>. Server-initiated frames
    /// arrive on the standalone GET stream. The session id and negotiated protocol version are captured
    /// here and replayed as headers.
    /// </summary>
    public sealed class HttpClientChannel : IMessageChannel
    {
        private readonly IStreamableHttpClient _http;
        private readonly IJson _json;
        private readonly JsonRpcCodec _codec;
        private readonly ILogger _logger;

        private string _sessionId;
        private string _protocolVersion;
        private string _lastEventId;
        private int _streamOpened;
        private CancellationTokenSource _streamCts;

        public HttpClientChannel(IStreamableHttpClient http, IJson json, ILoggerFactory loggerFactory)
        {
            _http = http;
            _json = json;
            _codec = new JsonRpcCodec(json);
            _logger = loggerFactory.Create<HttpClientChannel>();
        }

        public event Action<string> FrameReceived;

        public Task Start(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Stop()
        {
            _streamCts?.Cancel();
            var sessionId = _sessionId;
            return string.IsNullOrEmpty(sessionId)
                ? Task.CompletedTask
                : _http.DeleteSession(sessionId, _protocolVersion);
        }

        public async Task Send(string frame, CancellationToken cancellationToken = default)
        {
            // Only a request expects a reply on its own POST; notifications and our responses to
            // server-initiated requests are acknowledged with 202 and carry no body to feed back.
            var isRequest = _codec.TryDecode(frame, out var message) && message.Kind == JsonRpcMessageKind.Request;

            var reply = await _http.PostMessage(frame, _sessionId, _protocolVersion, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(reply.SessionId))
                _sessionId = reply.SessionId;

            if (!isRequest)
                return;

            if (reply.StatusCode == 404)
                throw new ClientException("The MCP session has expired; the client must reinitialize.");
            if (reply.StatusCode >= 400)
                throw new ClientException($"The server rejected the request with HTTP {reply.StatusCode}.");
            if (string.IsNullOrEmpty(reply.Body))
                throw new ClientException($"The server returned no JSON-RPC response (HTTP {reply.StatusCode}).");

            CaptureProtocolVersion(reply.Body);
            MaybeOpenStream();

            // Hand the reply to the peer, which correlates it to the awaiting request.
            FrameReceived?.Invoke(reply.Body);
        }

        // The negotiated protocol version is echoed in the initialize result; capture it once so it can
        // be replayed as the MCP-Protocol-Version header on every subsequent request.
        private void CaptureProtocolVersion(string body)
        {
            if (_protocolVersion != null)
                return;
            try
            {
                var version = _json.Parse(body)["result"]?.AsObject()?["protocolVersion"]?.AsString();
                if (!string.IsNullOrEmpty(version))
                    _protocolVersion = version;
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Could not read protocolVersion from the initialize reply: {ex.Message}");
            }
        }

        private void MaybeOpenStream()
        {
            if (string.IsNullOrEmpty(_sessionId))
                return;
            if (Interlocked.Exchange(ref _streamOpened, 1) == 1)
                return;

            _streamCts = new CancellationTokenSource();
            _ = Task.Run(() => StreamLoop(_streamCts.Token));
        }

        // Keeps the server→client SSE stream open, reconnecting (resuming from the last event id) if it
        // drops, until the channel stops.
        private async Task StreamLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _http.OpenStream(_sessionId, _protocolVersion, _lastEventId, OnStreamEvent, cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested)
                    break;
                try { await Task.Delay(250, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }

        // Server→client frames from the SSE stream: just hand them to the peer, which classifies them
        // into RequestReceived / NotificationReceived.
        private void OnStreamEvent(string eventId, string json)
        {
            if (!string.IsNullOrEmpty(eventId))
                _lastEventId = eventId;
            FrameReceived?.Invoke(json);
        }
    }

    public sealed class StreamableHttpClientTransportFactory : ITransportFactory
    {
        private readonly IStreamableHttpClient _http;
        private readonly IJson _json;

        public StreamableHttpClientTransportFactory(IStreamableHttpClient http, IJson json)
        {
            _http = http;
            _json = json;
        }

        public ITransport Create(ILoggerFactory loggerFactory)
            => new JsonRpcPeer(new HttpClientChannel(_http, _json, loggerFactory), _json, loggerFactory);
    }

    public static class StreamableHttpClientBuilderExtensions
    {
        public static ClientBuilder WithStreamableHttpTransport(this ClientBuilder builder, IJson json, IStreamableHttpClient http)
        {
            builder.WithTransport(new StreamableHttpClientTransportFactory(http, json));
            return builder;
        }
    }
}
