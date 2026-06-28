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
    /// <see cref="ITransport"/> the whole SDK needs: it adds request/response correlation, id generation,
    /// and inbound dispatch on top of any <see cref="IMessageChannel"/>. It works purely in
    /// <see cref="JsonRpcMessage"/> models — the channel below renders them to and parses them from the
    /// wire — so the peer never touches JSON itself.
    ///
    /// This is the generalization of the old <c>JsonRpcTransport</c> base class: instead of each transport
    /// <em>inheriting</em> the JSON-RPC layer (which only worked for single-channel transports), every
    /// transport <em>composes</em> it — stdio and Streamable HTTP alike — by being an
    /// <see cref="IMessageChannel"/>. <see cref="McpServer"/> / <see cref="McpClient"/> keep depending on
    /// <see cref="ITransport"/> and are unaware which channel sits underneath.
    /// </summary>
    public sealed class JsonRpcPeer : ITransport
    {
        private readonly IMessageChannel _channel;
        private readonly ILogger _logger;
        private readonly object _gate = new object();
        private readonly Dictionary<RequestId, TaskCompletionSource<JsonRpcResponse>> _pending = new();

        private long _nextId;

        public JsonRpcPeer(IMessageChannel channel, ILoggerFactory loggerFactory)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _logger = loggerFactory.Create<JsonRpcPeer>();
        }

        public event RequestReceivedCallback RequestReceived;
        public event NotificationReceivedCallback NotificationReceived;

        public Task Start(CancellationToken cancellationToken = default)
        {
            _channel.MessageReceived += OnMessageReceived;
            return _channel.Start(cancellationToken);
        }

        public async Task Stop()
        {
            _channel.MessageReceived -= OnMessageReceived;

            // The connection is closing; fail any in-flight requests rather than letting them hang.
            List<TaskCompletionSource<JsonRpcResponse>> pending;
            lock (_gate)
            {
                pending = new List<TaskCompletionSource<JsonRpcResponse>>(_pending.Values);
                _pending.Clear();
            }
            foreach (var tcs in pending)
                tcs.TrySetCanceled();

            await _channel.Stop().ConfigureAwait(false);
        }

        public async Task<JsonRpcResponse> SendRequest(string method, Json payload, CancellationToken cancellationToken = default)
        {
            var id = new RequestId(Interlocked.Increment(ref _nextId));

            // Register the pending response BEFORE sending, so a channel that delivers the reply
            // synchronously (e.g. an in-memory loopback) can never complete it before we are listening.
            var tcs = new TaskCompletionSource<JsonRpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate)
                _pending[id] = tcs;

            await _channel.Send(new JsonRpcRequest(id, method, payload), cancellationToken).ConfigureAwait(false);

            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
                return await tcs.Task.ConfigureAwait(false);
        }

        public Task SendNotification(string notification, Json arguments = null, CancellationToken cancellationToken = default)
            => _channel.Send(new JsonRpcNotification(notification, arguments), cancellationToken);

        public Task SendResponse(JsonRpcResponse response, CancellationToken cancellationToken = default)
            => _channel.Send(response, cancellationToken);

        private void OnMessageReceived(JsonRpcMessage message)
        {
            try
            {
                switch (message)
                {
                    case JsonRpcNotification notification:
                        NotificationReceived?.Invoke(notification);
                        break;
                    case JsonRpcRequest request:
                        RequestReceived?.Invoke(request);
                        break;
                    case JsonRpcResponse response:
                        TaskCompletionSource<JsonRpcResponse> tcs;
                        lock (_gate)
                        {
                            if (_pending.TryGetValue(response.Id, out tcs))
                                _pending.Remove(response.Id);
                        }
                        tcs?.TrySetResult(response);
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
