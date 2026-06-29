using System;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Client.Transports
{
    /// <summary>
    /// The Streamable HTTP client transport: the HTTP/SSE wire boundary. Outbound messages are rendered
    /// and POSTed; because HTTP couples a request's response to its POST, a request's reply is parsed and
    /// surfaced through <see cref="JsonRpcTransport.OnMessageReceived"/> for the inherited engine to
    /// correlate — race-free, because the engine registers the pending reply before calling
    /// <see cref="SendMessage"/>. Server-initiated messages arrive on the standalone GET stream. The
    /// session id and negotiated protocol version are captured here and replayed as headers.
    /// </summary>
    public sealed class StreamableHttpTransport : JsonRpcTransport
    {
        private readonly IStreamableHttpClient _http;
        private readonly IJson _json;

        private string _sessionId;
        private string _protocolVersion;
        private string _lastEventId;
        private int _streamOpened;
        private CancellationTokenSource _streamCts;

        public StreamableHttpTransport(IStreamableHttpClient http, IJson json, ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
            _http = http;
            _json = json;
        }

        protected override Task OnStart(CancellationToken cancellationToken = default) => Task.CompletedTask;

        protected override Task OnStop()
        {
            _streamCts?.Cancel();
            var sessionId = _sessionId;
            return string.IsNullOrEmpty(sessionId)
                ? Task.CompletedTask
                : _http.DeleteSession(sessionId, _protocolVersion);
        }

        protected override async Task SendMessage(JsonRpcMessage message, CancellationToken cancellationToken = default)
        {
            // Only a request expects a reply on its own POST; notifications and our responses to
            // server-initiated requests are acknowledged with 202 and carry no body to feed back.
            var isRequest = message is JsonRpcRequest;
            var payload = _json.Stringify(message.WriteMembers);

            var reply = await _http.PostMessage(payload, _sessionId, _protocolVersion, cancellationToken).ConfigureAwait(false);
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

            // Hand the reply to the inherited engine, which correlates it to the awaiting request.
            if (JsonRpcMessage.TryParse(_json, reply.Body, out var replyMessage))
                OnMessageReceived(replyMessage);
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
                Logger.LogDebug($"Could not read protocolVersion from the initialize reply: {ex.Message}");
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
        // drops, until the transport stops.
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

        // Server→client frames from the SSE stream: parse and hand them to the engine, which classifies
        // them into RequestReceived / NotificationReceived.
        private void OnStreamEvent(string eventId, string json)
        {
            if (!string.IsNullOrEmpty(eventId))
                _lastEventId = eventId;
            if (JsonRpcMessage.TryParse(_json, json, out var message))
                OnMessageReceived(message);
        }
    }
}
