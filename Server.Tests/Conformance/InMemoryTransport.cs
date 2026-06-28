#nullable disable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// In-process loopback transport used by the conformance suite. It is no longer its own JSON-RPC
    /// engine: it composes the shared <see cref="JsonRpcPeer"/> over a paired <see cref="InMemoryChannel"/>,
    /// the same three-layer stack the real transports use. It re-exposes the channel's wire recording
    /// (<see cref="Sent"/> / <see cref="Received"/>) and raw-frame injection (<see cref="SendRaw"/>) so the
    /// existing tests keep driving a single object as both an <see cref="ITransport"/> and a wire probe.
    /// </summary>
    public sealed class InMemoryTransport : ITransport
    {
        private readonly InMemoryChannel _channel;
        private readonly JsonRpcPeer _peer;

        private InMemoryTransport(InMemoryChannel channel, ILoggerFactory loggerFactory)
        {
            _channel = channel;
            _peer = new JsonRpcPeer(channel, loggerFactory);
        }

        public List<string> Sent => _channel.Sent;
        public List<string> Received => _channel.Received;

        public static (InMemoryTransport client, InMemoryTransport server) CreatePair(IJson json, ILoggerFactory loggerFactory)
        {
            var (a, b) = InMemoryChannel.CreatePair(json);
            return (new InMemoryTransport(a, loggerFactory), new InMemoryTransport(b, loggerFactory));
        }

        /// <summary>Sends a raw, pre-serialized JSON-RPC frame (used to craft custom request ids).</summary>
        public Task SendRaw(string messageAsJson) => _channel.SendRaw(messageAsJson);

        public event RequestReceivedCallback RequestReceived
        {
            add => _peer.RequestReceived += value;
            remove => _peer.RequestReceived -= value;
        }

        public event NotificationReceivedCallback NotificationReceived
        {
            add => _peer.NotificationReceived += value;
            remove => _peer.NotificationReceived -= value;
        }

        public Task Start(CancellationToken cancellationToken = default) => _peer.Start(cancellationToken);

        public Task Stop() => _peer.Stop();

        public Task SendNotification(JsonRpcNotification notification, CancellationToken cancellationToken = default)
            => _peer.SendNotification(notification, cancellationToken);

        public Task<JsonRpcResponse> SendRequest(JsonRpcRequest request, CancellationToken cancellationToken = default)
            => _peer.SendRequest(request, cancellationToken);

        public Task SendResponse(JsonRpcResponse response, CancellationToken cancellationToken = default)
            => _peer.SendResponse(response, cancellationToken);
    }
}
