using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Server
{
    /// <summary>
    /// The Streamable HTTP server transport for one MCP session. On top of the inherited
    /// <see cref="JsonRpcTransport"/> engine (correlation + dispatch), it owns the genuinely
    /// HTTP-specific concerns:
    /// <list type="bullet">
    ///   <item>routing an outbound frame — a JSON-RPC <em>response</em> goes back on the POST that
    ///   carried its request; a server-initiated request/notification goes onto the SSE stream;</item>
    ///   <item>the SSE event-id replay buffer for <c>Last-Event-ID</c> resumption.</item>
    /// </list>
    /// The HTTP listener drives it directly through <see cref="HandleInboundPost"/> and
    /// <see cref="AttachStream"/>, while the <c>McpServer</c> sees it only as an <see cref="ITransport"/>.
    /// </summary>
    public sealed class HttpServerTransport : JsonRpcTransport
    {
        private const int MaxBufferedEvents = 256;

        private readonly IJson _json;
        private readonly object _gate = new object();

        // Inbound client requests awaiting their HTTP response: id -> the POST's completion.
        private readonly Dictionary<RequestId, TaskCompletionSource<string>> _pendingPostById = new();

        // SSE events retained for Last-Event-ID resumption (bounded ring of the most recent ones).
        private readonly List<(long Id, string Json)> _eventBuffer = new();
        private readonly CancellationTokenSource _lifetimeCts = new CancellationTokenSource();

        private Func<long, string, Task> _streamWriter;
        private long _nextEventId;
        private long _deliveredThrough;

        public HttpServerTransport(IJson json, ILoggerFactory loggerFactory, string sessionId, string origin = null) : base(loggerFactory)
        {
            _json = json;
            SessionId = sessionId;
            Origin = origin;
        }

        /// <summary>The <c>Mcp-Session-Id</c> this transport is bound to.</summary>
        public string SessionId { get; }

        /// <summary>
        /// The HTTP <c>Origin</c> header captured from the connection's initialize request (the request that
        /// created this session's transport), or <c>null</c> when none was sent. The Streamable HTTP host
        /// surfaces this as <c>session.Origin</c> so a <c>ConfigureSession</c> callback can vary per-session
        /// registrations by origin.
        /// </summary>
        public string Origin { get; }

        /// <summary>Fires when the session is torn down, so an open SSE stream can close promptly.</summary>
        public CancellationToken Lifetime => _lifetimeCts.Token;

        protected override Task OnStart(CancellationToken cancellationToken = default) => Task.CompletedTask;

        protected override Task OnStop()
        {
            // Session teardown (DELETE / listener stop): detach the stream, release any open POSTs, and
            // signal the GET loop to close. (The base has already failed any in-flight server→client
            // requests.)
            List<TaskCompletionSource<string>> pending;
            lock (_gate)
            {
                _streamWriter = null;
                pending = new List<TaskCompletionSource<string>>(_pendingPostById.Values);
                _pendingPostById.Clear();
            }
            foreach (var tcs in pending)
                tcs.TrySetResult(null); // unblock the POST handler; it answers with no body

            try { _lifetimeCts.Cancel(); } catch { /* already disposed */ }
            return Task.CompletedTask;
        }

        // -- Outbound (engine -> wire) -------------------------------------------------------

        protected override Task SendMessage(JsonRpcMessage message, CancellationToken cancellationToken = default)
        {
            var payload = _json.Stringify(message.WriteMembers);

            // A response goes back on the POST that carried its request; everything else (a
            // server-initiated request or a notification) goes onto the SSE stream.
            if (message is JsonRpcResponse response)
            {
                TaskCompletionSource<string> tcs = null;
                lock (_gate)
                {
                    if (_pendingPostById.TryGetValue(response.Id, out tcs))
                        _pendingPostById.Remove(response.Id);
                }
                if (tcs != null)
                {
                    tcs.TrySetResult(payload);
                    return Task.CompletedTask;
                }
                // No POST is waiting (it already closed) — fall through to the stream.
            }

            return EmitEvent(payload);
        }

        // -- Inbound (HTTP listener -> engine) -----------------------------------------------

        /// <summary>
        /// Hands an inbound POST body to the engine. Returns the JSON-RPC response to write back on this
        /// POST (when the body is a request), or <c>null</c> for a notification/response (answered 202).
        /// </summary>
        public Task<string> HandleInboundPost(string body)
        {
            if (JsonRpcFraming.IsBatch(body))
            {
                Logger.LogError("Rejected a JSON-RPC batch; batching was removed in MCP 2025-06-18.");
                return Task.FromResult<string>(null);
            }

            if (!JsonRpcMessage.TryParse(_json, body, out var message))
            {
                Logger.LogDebug("Ignored an unparseable or non-dispatchable frame.");
                return Task.FromResult<string>(null);
            }

            if (message is JsonRpcRequest request)
            {
                // Hold the POST open until the server answers — its SendResponse routes the response back
                // here via SendMessage. Register before dispatch so a synchronous reply lands.
                var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                lock (_gate)
                    _pendingPostById[request.Id] = tcs;

                OnMessageReceived(message);
                return tcs.Task;
            }

            // A notification, or the client's reply to a server-initiated request: dispatch, answer 202.
            OnMessageReceived(message);
            return Task.FromResult<string>(null);
        }

        // -- SSE stream ----------------------------------------------------------------------

        /// <summary>
        /// Attaches the writer for this session's server→client SSE stream (the client's <c>GET</c>),
        /// resuming after <paramref name="lastEventId"/> when supplied — otherwise after the highest
        /// event already delivered, which replays anything produced before the first stream attached. The
        /// writer receives <c>(eventId, json)</c>. Returns a handle that detaches the stream on dispose.
        /// </summary>
        public IDisposable AttachStream(string lastEventId, Func<long, string, Task> writeEvent)
        {
            if (writeEvent == null)
                throw new ArgumentNullException(nameof(writeEvent));

            List<(long Id, string Json)> replay;
            lock (_gate)
            {
                _streamWriter = writeEvent;
                var resumeFrom = TryParseEventId(lastEventId, out var parsed) ? parsed : _deliveredThrough;
                replay = new List<(long, string)>();
                foreach (var ev in _eventBuffer)
                    if (ev.Id > resumeFrom)
                        replay.Add(ev);
                if (replay.Count > 0)
                    _deliveredThrough = Math.Max(_deliveredThrough, replay[replay.Count - 1].Id);
            }

            foreach (var ev in replay)
                _ = writeEvent(ev.Id, ev.Json);

            return new StreamHandle(this, writeEvent);
        }

        private static bool TryParseEventId(string value, out long id)
        {
            id = 0;
            return !string.IsNullOrEmpty(value) && long.TryParse(value, out id);
        }

        // Assigns a monotonic SSE event id, retains the event for resumption, and writes it to the
        // attached stream (if any). Events produced before a stream attaches wait in the buffer.
        private Task EmitEvent(string messageJson)
        {
            long eventId;
            Func<long, string, Task> writer;
            lock (_gate)
            {
                eventId = ++_nextEventId;
                _eventBuffer.Add((eventId, messageJson));
                if (_eventBuffer.Count > MaxBufferedEvents)
                    _eventBuffer.RemoveAt(0);

                writer = _streamWriter;
                if (writer == null)
                    return Task.CompletedTask;

                _deliveredThrough = eventId;
            }

            return writer(eventId, messageJson);
        }

        private void DetachStream(Func<long, string, Task> writeEvent)
        {
            lock (_gate)
            {
                if (ReferenceEquals(_streamWriter, writeEvent))
                    _streamWriter = null;
            }
        }

        private sealed class StreamHandle : IDisposable
        {
            private readonly HttpServerTransport _transport;
            private readonly Func<long, string, Task> _writeEvent;

            public StreamHandle(HttpServerTransport transport, Func<long, string, Task> writeEvent)
            {
                _transport = transport;
                _writeEvent = writeEvent;
            }

            public void Dispose() => _transport.DetachStream(_writeEvent);
        }
    }
}
