#nullable disable
using System;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Shared;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// An in-process loopback <see cref="IMessageChannel"/> — the dumb-pipe counterpart of
    /// <see cref="InMemoryTransport"/>. Two instances are linked as a pair; whatever one sends is
    /// delivered to the other's <see cref="FrameReceived"/> on a background task (mimicking a real async
    /// transport and avoiding re-entrancy). Used to prove the channel/peer layering with no OS pipe.
    /// </summary>
    public sealed class InMemoryChannel : IMessageChannel
    {
        private InMemoryChannel _peer;

        public event Action<string> FrameReceived;

        public Task Start(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Stop() => Task.CompletedTask;

        public Task Send(JsonRpcFrame frame, CancellationToken cancellationToken = default)
        {
            // Off the wire a channel only has bytes, so deliver the serialized payload as an inbound frame.
            var peer = _peer;
            _ = Task.Run(() => peer.FrameReceived?.Invoke(frame.Payload));
            return Task.CompletedTask;
        }

        public static (InMemoryChannel a, InMemoryChannel b) CreatePair()
        {
            var a = new InMemoryChannel();
            var b = new InMemoryChannel();
            a._peer = b;
            b._peer = a;
            return (a, b);
        }
    }
}
