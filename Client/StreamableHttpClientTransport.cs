using System;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Shared;

namespace McpSdk.Client
{
    /// <summary>
    /// Client-side MCP transport for the Streamable HTTP transport (single endpoint). A request POST is
    /// answered on the same HTTP response, so request/response correlation is synchronous within
    /// <see cref="SendRequest"/> — no background read loop is needed for the <c>application/json</c>
    /// case. The <c>Mcp-Session-Id</c> the server issues on initialize, and the negotiated protocol
    /// version it echoes, are captured and replayed as headers on every subsequent request.
    ///
    /// Server→client messages over the standalone <c>GET</c> stream (which drive
    /// <see cref="RequestReceived"/> / <see cref="NotificationReceived"/>) arrive in Phase G increment 2.
    /// </summary>
    public sealed class StreamableHttpClientTransport : ITransport
    {
        private readonly IStreamableHttpClient _http;
        private readonly IJson _json;
        private readonly JsonRpcCodec _codec;
        private readonly ILogger _logger;

        private long _nextId;
        private string _sessionId;
        private string _protocolVersion;
        private string _lastEventId;
        private int _streamOpened;
        private CancellationTokenSource _streamCts;

        public StreamableHttpClientTransport(IStreamableHttpClient http, IJson json, ILoggerFactory loggerFactory)
        {
            _http = http;
            _json = json;
            _codec = new JsonRpcCodec(json);
            _logger = loggerFactory.Create<StreamableHttpClientTransport>();
        }

        public event RequestReceivedCallback RequestReceived;
        public event NotificationReceivedCallback NotificationReceived;

        public Task Start(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Stop()
        {
            _streamCts?.Cancel();
            var sessionId = _sessionId;
            return string.IsNullOrEmpty(sessionId)
                ? Task.CompletedTask
                : _http.DeleteSession(sessionId, _protocolVersion);
        }

        public async Task<IResponse> SendRequest(string method, Json payload, CancellationToken cancellationToken = default)
        {
            var id = new RequestId(Interlocked.Increment(ref _nextId));
            var requestJson = _codec.EncodeRequest(id, method, payload);

            _logger.LogDebug($"POST request: {requestJson}");
            var reply = await _http.PostMessage(requestJson, _sessionId, _protocolVersion, cancellationToken).ConfigureAwait(false);
            CaptureSession(reply);

            if (reply.StatusCode == 404)
                throw new ClientException("The MCP session has expired; the client must reinitialize.");
            if (reply.StatusCode >= 400)
                throw new ClientException($"The server rejected the request with HTTP {reply.StatusCode}.");
            if (string.IsNullOrEmpty(reply.Body))
                throw new ClientException($"The server returned no JSON-RPC response (HTTP {reply.StatusCode}).");

            var responseObj = _json.Parse(reply.Body);
            CaptureProtocolVersion(responseObj);
            MaybeOpenStream();
            return _codec.ParseResponse(responseObj);
        }

        public async Task SendNotification(string notification, Json arguments = null, CancellationToken cancellationToken = default)
        {
            var json = _codec.EncodeNotification(notification, arguments);

            _logger.LogDebug($"POST notification: {json}");
            var reply = await _http.PostMessage(json, _sessionId, _protocolVersion, cancellationToken).ConfigureAwait(false);
            CaptureSession(reply);
        }

        public async Task SendOkResponse(RequestId requestId, Json writeResult, CancellationToken cancellationToken = default)
        {
            var json = _codec.EncodeResult(requestId, writeResult);
            await _http.PostMessage(json, _sessionId, _protocolVersion, cancellationToken).ConfigureAwait(false);
        }

        public async Task SendErrorResponse(RequestId requestId, Error error, CancellationToken cancellationToken = default)
        {
            var json = _codec.EncodeError(requestId, error);
            await _http.PostMessage(json, _sessionId, _protocolVersion, cancellationToken).ConfigureAwait(false);
        }

        private void CaptureSession(StreamableHttpResponse reply)
        {
            if (!string.IsNullOrEmpty(reply.SessionId))
                _sessionId = reply.SessionId;
        }

        // The negotiated protocol version is echoed in the initialize result. Capture it once so it can
        // be replayed as the MCP-Protocol-Version header on every subsequent request (spec requirement).
        private void CaptureProtocolVersion(IJsonObject responseObj)
        {
            if (_protocolVersion != null)
                return;

            var version = responseObj["result"]?.AsObject()?["protocolVersion"]?.AsString();
            if (!string.IsNullOrEmpty(version))
                _protocolVersion = version;
        }

        // Opens the standalone server→client SSE stream once, after initialization has yielded the
        // session id (and the negotiated version for its header). Idempotent.
        private void MaybeOpenStream()
        {
            if (string.IsNullOrEmpty(_sessionId))
                return;
            if (Interlocked.Exchange(ref _streamOpened, 1) == 1)
                return;

            _streamCts = new CancellationTokenSource();
            _ = Task.Run(() => StreamLoop(_streamCts.Token));
        }

        // Keeps the server→client SSE stream open, reconnecting (resuming from the last seen event id)
        // if it drops, until the transport stops.
        private async Task StreamLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _http.OpenStream(_sessionId, _protocolVersion, _lastEventId, OnStreamEvent, cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested)
                    break;

                // The stream ended on its own; back off briefly, then resume from _lastEventId.
                try { await Task.Delay(250, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }

        // Dispatches a server→client frame from the SSE stream. Only requests and notifications arrive
        // here; responses to the client's own requests come back synchronously on their POST.
        private void OnStreamEvent(string eventId, string messageJson)
        {
            if (!string.IsNullOrEmpty(eventId))
                _lastEventId = eventId;

            try
            {
                if (!_codec.TryDecode(messageJson, out var message))
                    return;

                switch (message.Kind)
                {
                    case JsonRpcMessageKind.Notification:
                        NotificationReceived?.Invoke(message.Method, message.Parameters);
                        break;
                    case JsonRpcMessageKind.Request:
                        RequestReceived?.Invoke(message.Id, message.Method, message.Parameters);
                        break;
                    // A response would be a stray frame on this stream; nothing to correlate.
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
            }
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

        public ITransport Create(ILoggerFactory loggerFactory) => new StreamableHttpClientTransport(_http, _json, loggerFactory);
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
