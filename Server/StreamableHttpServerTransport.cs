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

        private Func<string, Task> _streamWriter;

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

        // ITransport lifecycle: the HTTP listener owns the socket, so there is nothing to start/stop here.
        public Task Start(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Stop() => Task.CompletedTask;

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

            // A client→server response (a reply to a server-initiated request). Outbound-request
            // correlation arrives with the SSE stream; acknowledge with 202 for now.
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
            return PushToStream(message);
        }

        // Server-initiated request → the client's SSE stream. Response correlation lands with the
        // standalone GET stream (Phase G increment 2); the core tools flow never reaches this path.
        public Task<IResponse> SendRequest(string method, Json request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "Server-initiated requests over Streamable HTTP require the SSE stream (Phase G increment 2).");
        }

        /// <summary>
        /// Attaches the writer for this session's server→client SSE stream (the client's <c>GET</c>).
        /// Returns a handle that detaches the stream when disposed.
        /// </summary>
        public IDisposable AttachStream(Func<string, Task> writeEvent)
        {
            lock (_gate)
                _streamWriter = writeEvent ?? throw new ArgumentNullException(nameof(writeEvent));
            return new StreamHandle(this, writeEvent);
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
                _ = PushToStream(response); // no POST is waiting; fall back to the stream
        }

        private Task PushToStream(string messageJson)
        {
            Func<string, Task> writer;
            lock (_gate)
                writer = _streamWriter;

            if (writer == null)
            {
                _logger.LogDebug("No SSE stream is attached; dropping a server→client message.");
                return Task.CompletedTask;
            }

            return writer(messageJson);
        }

        private void DetachStream(Func<string, Task> writeEvent)
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
            private readonly Func<string, Task> _writeEvent;

            public StreamHandle(StreamableHttpServerTransport transport, Func<string, Task> writeEvent)
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
