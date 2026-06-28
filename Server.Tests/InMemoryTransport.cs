#nullable disable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Server.Tests
{
    /// <summary>
    /// In-process loopback transport used by the conformance suite — a <see cref="JsonRpcTransport"/> whose
    /// wire is simply its paired twin. Two instances are linked; whatever one sends is rendered to the
    /// wire, re-parsed, and delivered to the other on a background task (mimicking a real async transport,
    /// exercising real serialization, and avoiding re-entrancy). Every raw JSON line sent and received is
    /// recorded (<see cref="Sent"/> / <see cref="Received"/>) so tests can assert on the exact wire format,
    /// and <see cref="SendRaw"/> injects a hand-crafted frame (e.g. a custom string id, or a batch).
    /// </summary>
    public sealed class InMemoryTransport : JsonRpcTransport
    {
        private readonly IJson _json;
        private InMemoryTransport _peer;

        public List<string> Sent { get; } = new();
        public List<string> Received { get; } = new();

        private InMemoryTransport(IJson json, ILoggerFactory loggerFactory) : base(loggerFactory)
        {
            _json = json;
        }

        public static (InMemoryTransport client, InMemoryTransport server) CreatePair(IJson json, ILoggerFactory loggerFactory)
        {
            var client = new InMemoryTransport(json, loggerFactory);
            var server = new InMemoryTransport(json, loggerFactory);
            client._peer = server;
            server._peer = client;
            return (client, server);
        }

        /// <summary>Sends a raw, pre-serialized JSON-RPC frame (used to craft custom request ids).</summary>
        public Task SendRaw(string messageAsJson) => Deliver(messageAsJson);

        protected override Task OnStart(CancellationToken cancellationToken = default) => Task.CompletedTask;

        protected override Task OnStop() => Task.CompletedTask;

        protected override Task SendMessage(JsonRpcMessage message, CancellationToken cancellationToken = default)
            => Deliver(_json.Stringify(message.WriteMembers));

        private Task Deliver(string wire)
        {
            lock (Sent)
                Sent.Add(wire);

            var peer = _peer;
            _ = Task.Run(() =>
            {
                lock (peer.Received)
                    peer.Received.Add(wire);
                if (JsonRpcMessage.TryParse(_json, wire, out var inbound))
                    peer.OnMessageReceived(inbound);
            });
            return Task.CompletedTask;
        }
    }
}
