#nullable disable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// An in-process loopback <see cref="IMessageChannel"/> — the dumb-pipe seam under
    /// <see cref="JsonRpcPeer"/>. Two instances are linked as a pair; whatever one sends is rendered to the
    /// wire, re-parsed, and delivered to the other's <see cref="MessageReceived"/> on a background task
    /// (mimicking a real async transport, exercising real serialization, and avoiding re-entrancy).
    ///
    /// Every raw JSON line sent and received is recorded (<see cref="Sent"/> / <see cref="Received"/>) so
    /// tests can assert on the exact wire format, and <see cref="SendRaw"/> injects a hand-crafted frame
    /// (e.g. a custom string id, or a batch) straight onto the wire, bypassing the message model.
    /// </summary>
    public sealed class InMemoryChannel : IMessageChannel
    {
        private readonly IJson _json;
        private InMemoryChannel _peer;

        public List<string> Sent { get; } = new();
        public List<string> Received { get; } = new();

        private InMemoryChannel(IJson json)
        {
            _json = json;
        }

        public event Action<JsonRpcMessage> MessageReceived;

        public Task Start(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Stop() => Task.CompletedTask;

        public Task Send(JsonRpcMessage message, CancellationToken cancellationToken = default)
            => Deliver(_json.Stringify(message.WriteMembers));

        /// <summary>Injects a raw, pre-serialized frame (used to craft custom ids / malformed input).</summary>
        public Task SendRaw(string wire) => Deliver(wire);

        private Task Deliver(string wire)
        {
            lock (Sent)
                Sent.Add(wire);

            // Round-trip through the wire so the loopback exercises encode + decode, just like a real pipe.
            var peer = _peer;
            _ = Task.Run(() =>
            {
                lock (peer.Received)
                    peer.Received.Add(wire);
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
