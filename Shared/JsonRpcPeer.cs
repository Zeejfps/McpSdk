using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Shared
{
    /// <summary>
    /// The single, transport-independent JSON-RPC engine — the one implementation of
    /// <see cref="ITransport"/> the whole SDK needs. It adds request/response correlation and inbound
    /// dispatch on top of any <see cref="IMessageChannel"/> (the sender stamps each request id; the peer
    /// only matches the reply back to it). It works purely in <see cref="JsonRpcMessage"/> models — the
    /// channel below renders them to and parses them from the wire — so the peer never touches JSON itself.
    ///
    /// Every transport <em>composes</em> this engine rather than reimplementing it: each one is just an
    /// <see cref="IMessageChannel"/> — stdio and Streamable HTTP alike — wrapped in a peer.
    /// <see cref="McpServer"/> / <see cref="McpClient"/> keep depending on <see cref="ITransport"/> and are
    /// unaware which channel sits underneath.
    /// </summary>
    public sealed class JsonRpcPeer : ITransport
    {
        private readonly IMessageChannel _channel;
        private readonly ILogger _logger;
        private readonly object _lock = new();
        private readonly Dictionary<RequestId, TaskCompletionSource<JsonRpcResponse>> _pending = new();

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
            lock (_lock)
            {
                pending = new List<TaskCompletionSource<JsonRpcResponse>>(_pending.Values);
                _pending.Clear();
            }
            foreach (var tcs in pending)
                tcs.TrySetCanceled();

            await _channel.Stop().ConfigureAwait(false);
        }

        public async Task<JsonRpcResponse> SendRequest(JsonRpcRequest request, CancellationToken cancellationToken = default)
        {
            // Register the pending response BEFORE sending, so a channel that delivers the reply
            // synchronously (e.g. an in-memory loopback) can never complete it before we are listening.
            var tcs = new TaskCompletionSource<JsonRpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_lock)
                _pending[request.Id] = tcs;

            await _channel.Send(request, cancellationToken).ConfigureAwait(false);

            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
                return await tcs.Task.ConfigureAwait(false);
        }

        public Task SendNotification(JsonRpcNotification notification, CancellationToken cancellationToken = default)
            => _channel.Send(notification, cancellationToken);

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
                        lock (_lock)
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
