using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Shared
{
    /// <summary>
    /// The single, transport-independent JSON-RPC engine. It is the one implementation of
    /// <see cref="ITransport"/> the whole SDK needs: it adds request/response correlation, id
    /// generation, and inbound dispatch on top of any <see cref="IMessageChannel"/>, encoding and
    /// decoding the wire format through <see cref="JsonRpcCodec"/>.
    ///
    /// This is the generalization of the old <c>JsonRpcTransport</c> base class: instead of each
    /// transport <em>inheriting</em> the JSON-RPC layer (which only worked for single-channel
    /// transports), every transport <em>composes</em> it — stdio and Streamable HTTP alike — by being an
    /// <see cref="IMessageChannel"/>. <see cref="McpServer"/> / <see cref="McpClient"/> keep depending on
    /// <see cref="ITransport"/> and are unaware which channel sits underneath.
    /// </summary>
    public sealed class JsonRpcPeer : ITransport
    {
        private readonly IMessageChannel _channel;
        private readonly JsonRpcCodec _codec;
        private readonly ILogger _logger;
        private readonly object _gate = new object();
        private readonly Dictionary<RequestId, TaskCompletionSource<IJsonObject>> _pending = new();

        private long _nextId;

        public JsonRpcPeer(IMessageChannel channel, IJson json, ILoggerFactory loggerFactory)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _codec = new JsonRpcCodec(json);
            _logger = loggerFactory.Create<JsonRpcPeer>();
        }

        public event RequestReceivedCallback RequestReceived;
        public event NotificationReceivedCallback NotificationReceived;

        public Task Start(CancellationToken cancellationToken = default)
        {
            _channel.FrameReceived += OnFrameReceived;
            return _channel.Start(cancellationToken);
        }

        public async Task Stop()
        {
            _channel.FrameReceived -= OnFrameReceived;

            // The connection is closing; fail any in-flight requests rather than letting them hang.
            List<TaskCompletionSource<IJsonObject>> pending;
            lock (_gate)
            {
                pending = new List<TaskCompletionSource<IJsonObject>>(_pending.Values);
                _pending.Clear();
            }
            foreach (var tcs in pending)
                tcs.TrySetCanceled();

            await _channel.Stop().ConfigureAwait(false);
        }

        public async Task<IResponse> SendRequest(string method, Json payload, CancellationToken cancellationToken = default)
        {
            var id = new RequestId(Interlocked.Increment(ref _nextId));

            // Register the pending response BEFORE sending, so a channel that delivers the reply
            // synchronously (e.g. an in-memory loopback) can never complete it before we are listening.
            var tcs = new TaskCompletionSource<IJsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate)
                _pending[id] = tcs;

            await _channel.Send(_codec.EncodeRequest(id, method, payload), cancellationToken).ConfigureAwait(false);

            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
                return _codec.ParseResponse(await tcs.Task.ConfigureAwait(false));
        }

        public Task SendNotification(string notification, Json arguments = null, CancellationToken cancellationToken = default)
            => _channel.Send(_codec.EncodeNotification(notification, arguments), cancellationToken);

        public Task SendOkResponse(RequestId requestId, Json result, CancellationToken cancellationToken = default)
            => _channel.Send(_codec.EncodeResult(requestId, result), cancellationToken);

        public Task SendErrorResponse(RequestId requestId, Error error, CancellationToken cancellationToken = default)
            => _channel.Send(_codec.EncodeError(requestId, error), cancellationToken);

        private void OnFrameReceived(string frame)
        {
            try
            {
                // Batching was removed in 2025-06-18; reject a top-level array explicitly.
                if (JsonRpcFraming.IsBatch(frame))
                {
                    _logger.LogError("Rejected a JSON-RPC batch message; batching was removed in MCP 2025-06-18.");
                    return;
                }

                if (!_codec.TryDecode(frame, out var message))
                    return;

                switch (message.Kind)
                {
                    case JsonRpcMessageKind.Notification:
                        NotificationReceived?.Invoke(message.Method, message.Parameters);
                        break;
                    case JsonRpcMessageKind.Request:
                        RequestReceived?.Invoke(message.Id, message.Method, message.Parameters);
                        break;
                    case JsonRpcMessageKind.Response:
                        TaskCompletionSource<IJsonObject> tcs;
                        lock (_gate)
                        {
                            if (_pending.TryGetValue(message.Id, out tcs))
                                _pending.Remove(message.Id);
                        }
                        tcs?.TrySetResult(message.Raw);
                        break;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e);
            }
        }
    }
}
