#nullable disable
using System;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// An in-process loopback <see cref="IMessageChannel"/> — the dumb-pipe counterpart of
    /// <see cref="InMemoryTransport"/>. Two instances are linked as a pair; whatever one sends is rendered
    /// to the wire, re-parsed, and delivered to the other's <see cref="MessageReceived"/> on a background
    /// task (mimicking a real async transport, exercising real serialization, and avoiding re-entrancy).
    /// </summary>
    public sealed class InMemoryChannel : IMessageChannel
    {
        private readonly IJson _json;
        private InMemoryChannel _peer;

        private InMemoryChannel(IJson json)
        {
            _json = json;
        }

        public event Action<JsonRpcMessage> MessageReceived;

        public Task Start(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Stop() => Task.CompletedTask;

        public Task Send(JsonRpcMessage message, CancellationToken cancellationToken = default)
        {
            // Round-trip through the wire so the loopback exercises encode + decode, just like a real pipe.
            var wire = _json.Stringify(message.WriteMembers);
            var peer = _peer;
            _ = Task.Run(() =>
            {
                if (JsonRpcMessage.TryParse(peer._json, wire, out var inbound))
                    peer.MessageReceived?.Invoke(inbound);
            });
            return Task.CompletedTask;
        }

        public static (InMemoryChannel a, InMemoryChannel b) CreatePair(IJson json)
        {
            var a = new InMemoryChannel(json);
            var b = new InMemoryChannel(json);
            a._peer = b;
            b._peer = a;
            return (a, b);
        }
    }
}
