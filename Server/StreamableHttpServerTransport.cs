using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Shared;

namespace McpSdk.Server
{
    /// <summary>
    /// Server-side MCP transport for the Streamable HTTP transport (2025-03-26+, single endpoint).
    ///
    /// Unlike the legacy dual-endpoint SSE transport — a symmetric channel that fit
    /// <see cref="JsonRpcTransport"/>'s one-<c>Send</c>/one-receive model — Streamable HTTP correlates
    /// each response to the POST that carried its request. So this implements <see cref="ITransport"/>
    /// directly: <see cref="Deliver"/> hands an inbound client frame to the server and returns the
    /// JSON-RPC response to write back on that same POST (or <c>null</c> for a notification/response,
    /// which the HTTP layer acknowledges with 202). Server-initiated requests/notifications are pushed
    /// onto the client's SSE stream via <see cref="AttachStream"/>.
    ///
    /// One instance corresponds to one MCP session (one <c>Mcp-Session-Id</c>); the HTTP listener owns
    /// the socket and routes POSTs for a session to its transport.
    /// </summary>
    public sealed class StreamableHttpServerTransport : ITransport
    {
        private const string JsonRpcVersion = "2.0";

        private readonly IJson _json;
        private readonly ILogger _logger;
        private readonly object _gate = new object();

        // Inbound client requests awaiting a response from the server, keyed by JSON-RPC id. Each entry
        // completes when McpServer answers via SendOkResponse/SendErrorResponse, unblocking the POST.
        private readonly Dictionary<RequestId, TaskCompletionSource<string>> _pendingByRequestId = new();

        // Server-initiated requests awaiting the client's response (which arrives as a separate POST).
        private readonly Dictionary<RequestId, TaskCompletionSource<IJsonObject>> _pendingOutgoingByRequestId = new();

        // Server→client SSE events, retained for Last-Event-ID resumption (a bounded ring of the most
        // recent ones). Also covers the "produced before the first stream attached" case via replay.
        private readonly List<(long Id, string Json)> _eventBuffer = new();

        private Func<long, string, Task> _streamWriter;
        private long _nextOutgoingId;
        private long _nextEventId;
        private long _deliveredThrough;

        private const int MaxBufferedEvents = 256;

        public StreamableHttpServerTransport(IJson json, ILoggerFactory loggerFactory, string sessionId)
        {
            _json = json;
            _logger = loggerFactory.Create<StreamableHttpServerTransport>();
            SessionId = sessionId;
        }

        public event RequestReceivedCallback RequestReceived;
        public event NotificationReceivedCallback NotificationReceived;

        /// <summary>The <c>Mcp-Session-Id</c> this transport is bound to.</summary>
        public string SessionId { get; }

        private readonly CancellationTokenSource _lifetimeCts = new CancellationTokenSource();

        // ITransport lifecycle: the HTTP listener owns the socket, so there is nothing to start/stop here.
        public Task Start(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Stop() => Task.CompletedTask;

        /// <summary>Fires when the session is terminated, so an open SSE stream can close promptly.</summary>
        public CancellationToken Lifetime => _lifetimeCts.Token;

        /// <summary>
        /// Tears the session down (client <c>DELETE</c>): detaches the stream, cancels any in-flight
        /// requests, and signals <see cref="Lifetime"/> so the open SSE stream closes.
        /// </summary>
        public void Terminate()
        {
            List<TaskCompletionSource<string>> inbound;
            List<TaskCompletionSource<IJsonObject>> outbound;
            lock (_gate)
            {
                _streamWriter = null;
                inbound = new List<TaskCompletionSource<string>>(_pendingByRequestId.Values);
                _pendingByRequestId.Clear();
                outbound = new List<TaskCompletionSource<IJsonObject>>(_pendingOutgoingByRequestId.Values);
                _pendingOutgoingByRequestId.Clear();
            }

            foreach (var tcs in inbound)
                tcs.TrySetCanceled();
            foreach (var tcs in outbound)
                tcs.TrySetCanceled();

            try { _lifetimeCts.Cancel(); } catch { /* already disposed */ }
        }

        /// <summary>
        /// Hands an inbound client→server JSON-RPC frame to the server. Returns the JSON-RPC response to
        /// write back on the originating POST (<c>Content-Type: application/json</c>), or <c>null</c>
        /// when the frame is a notification or a response — those carry no body and are answered with 202.
        /// </summary>
        public Task<string> Deliver(string messageJson)
        {
            // Batching was removed in 2025-06-18; a top-level array is no longer a valid frame.
            if (JsonRpcFraming.IsBatch(messageJson))
            {
                _logger.LogError("Rejected a JSON-RPC batch; batching was removed in MCP 2025-06-18.");
                return Task.FromResult<string>(null);
            }

            IJsonObject message;
            try
            {
                message = _json.Parse(messageJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                return Task.FromResult<string>(null);
            }

            var idProp = message["id"];
            var method = message["method"]?.AsString();
            var methodParams = message["params"]?.AsObject();

            if (method != null)
            {
                if (idProp == null)
                {
                    // Notification: dispatch and acknowledge with 202 (no body).
                    NotificationReceived?.Invoke(method, methodParams);
                    return Task.FromResult<string>(null);
                }

                // Request: reserve a slot for its response, then dispatch. The POST stays open until the
                // server answers via SendOkResponse/SendErrorResponse, which completes this task.
                var id = RequestId.FromJson(idProp);
                var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                lock (_gate)
                    _pendingByRequestId[id] = tcs;

                RequestReceived?.Invoke(id, method, methodParams);
                return tcs.Task;
            }

            // A client→server response: the reply to a server-initiated request. Complete the pending
            // outgoing request and acknowledge the POST with 202.
            if (idProp != null)
            {
                var responseId = RequestId.FromJson(idProp);
                TaskCompletionSource<IJsonObject> outgoing;
                lock (_gate)
                {
                    if (_pendingOutgoingByRequestId.TryGetValue(responseId, out outgoing))
                        _pendingOutgoingByRequestId.Remove(responseId);
                }
                outgoing?.TrySetResult(message);
            }
            return Task.FromResult<string>(null);
        }

        public Task SendOkResponse(RequestId requestId, Json writeResult, CancellationToken cancellationToken = default)
        {
            var response = _json.Stringify(req =>
            {
                JsonRpcVersion.WriteTo(req, "jsonrpc");
                requestId.WriteTo(req, "id");
                writeResult.WriteTo(req, "result");
            });
            CompletePending(requestId, response);
            return Task.CompletedTask;
        }

        public Task SendErrorResponse(RequestId requestId, Error error, CancellationToken cancellationToken = default)
        {
            var response = _json.Stringify(req =>
            {
                JsonRpcVersion.WriteTo(req, "jsonrpc");
                requestId.WriteTo(req, "id");
                error.WriteTo(req, "error");
            });
            CompletePending(requestId, response);
            return Task.CompletedTask;
        }

        // Server-initiated notification → the client's SSE stream (dropped if no stream is attached).
        public Task SendNotification(string notification, Json arguments = null, CancellationToken cancellationToken = default)
        {
            var message = _json.Stringify(req =>
            {
                JsonRpcVersion.WriteTo(req, "jsonrpc");
                notification.WriteTo(req, "method");
                if (arguments != null)
                    arguments.WriteTo(req, "params");
            });
            return EmitEvent(message);
        }

        // Server-initiated request → the client's SSE stream; the response returns as a client POST.
        public async Task<IResponse> SendRequest(string method, Json request, CancellationToken cancellationToken = default)
        {
            var id = new RequestId(Interlocked.Increment(ref _nextOutgoingId));
            var frame = _json.Stringify(req =>
            {
                JsonRpcVersion.WriteTo(req, "jsonrpc");
                id.WriteTo(req, "id");
                method.WriteTo(req, "method");
                request.WriteTo(req, "params");
            });

            var tcs = new TaskCompletionSource<IJsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate)
                _pendingOutgoingByRequestId[id] = tcs;

            await EmitEvent(frame).ConfigureAwait(false);
            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                var responseObj = await tcs.Task.ConfigureAwait(false);
                return ParseResponse(responseObj);
            }
        }

        /// <summary>
        /// Attaches the writer for this session's server→client SSE stream (the client's <c>GET</c>),
        /// resuming after <paramref name="lastEventId"/> when the client supplied one. With no
        /// <c>Last-Event-ID</c> it resumes after the highest event already delivered to a stream, which
        /// replays any events produced before the first stream attached. The writer receives
        /// <c>(eventId, json)</c>. Returns a handle that detaches the stream when disposed.
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

            // Replay the missed tail in order before live events flow.
            foreach (var ev in replay)
                _ = writeEvent(ev.Id, ev.Json);

            return new StreamHandle(this, writeEvent);
        }

        private static bool TryParseEventId(string value, out long id)
        {
            id = 0;
            return !string.IsNullOrEmpty(value) && long.TryParse(value, out id);
        }

        private void CompletePending(RequestId requestId, string response)
        {
            TaskCompletionSource<string> tcs = null;
            lock (_gate)
            {
                if (_pendingByRequestId.TryGetValue(requestId, out tcs))
                    _pendingByRequestId.Remove(requestId);
            }

            if (tcs != null)
                tcs.TrySetResult(response);
            else
                _ = EmitEvent(response); // no POST is waiting; fall back to the stream
        }

        // Assigns a monotonic SSE event id, retains the event for resumption, and writes it to the
        // attached stream (if any). Events produced before a stream attaches wait in the buffer and are
        // replayed on attach.
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

        private IResponse ParseResponse(IJsonObject response)
        {
            var errorProp = response["error"];
            if (errorProp == null)
                return Response.FromResult(response["result"].AsObject());
            return Response.FromError(new Error(errorProp.AsObject()));
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
            private readonly StreamableHttpServerTransport _transport;
            private readonly Func<long, string, Task> _writeEvent;

            public StreamHandle(StreamableHttpServerTransport transport, Func<long, string, Task> writeEvent)
            {
                _transport = transport;
                _writeEvent = writeEvent;
            }

            public void Dispose() => _transport.DetachStream(_writeEvent);
        }
    }

    public static class StreamableHttpServerBuilderExtensions
    {
        /// <summary>
        /// Wires a pre-built per-session <see cref="StreamableHttpServerTransport"/> (created by the HTTP
        /// listener) into the server. Used from the listener's session callback, mirroring how the old
        /// SSE adapter wired a per-connection session.
        /// </summary>
        public static ServerBuilder WithStreamableHttpTransport(this ServerBuilder builder, ITransport transport)
        {
            builder.WithTransport(new ExistingTransportFactory(transport));
            return builder;
        }
    }

    internal sealed class ExistingTransportFactory : ITransportFactory
    {
        private readonly ITransport _transport;

        public ExistingTransportFactory(ITransport transport)
        {
            _transport = transport;
        }

        public ITransport Create(ILoggerFactory loggerFactory) => _transport;
    }
}
