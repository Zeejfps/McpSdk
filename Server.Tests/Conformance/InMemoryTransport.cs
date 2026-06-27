using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// In-process loopback transport used by the conformance suite. Two instances are linked as a
    /// pair; whatever one sends is delivered to the other's message pump on a background task (to
    /// mimic a real async transport and avoid re-entrancy). Every raw JSON line sent and received
    /// is recorded so tests can assert on the exact wire format (e.g. the negotiated version or the
    /// echoed request id).
    /// </summary>
    public sealed class InMemoryTransport : JsonRpcTransport
    {
        private InMemoryTransport _peer;

        public List<string> Sent { get; } = new();
        public List<string> Received { get; } = new();

        public InMemoryTransport(IJson json, ILoggerFactory loggerFactory) : base(json, loggerFactory)
        {
        }

        public static (InMemoryTransport client, InMemoryTransport server) CreatePair(IJson json, ILoggerFactory loggerFactory)
        {
            var client = new InMemoryTransport(json, loggerFactory);
            var server = new InMemoryTransport(json, loggerFactory);
            client._peer = server;
            server._peer = client;
            return (client, server);
        }

        /// <summary>Sends a raw, pre-serialized JSON-RPC message (used to craft custom request ids).</summary>
        public Task SendRaw(string messageAsJson) => Send(messageAsJson);

        protected override Task OnStart(CancellationToken cancellationToken = default) => Task.CompletedTask;

        protected override Task OnStop(CancellationToken cancellationToken = default) => Task.CompletedTask;

        protected override Task Send(string requestAsJson, CancellationToken cancellationToken = default)
        {
            lock (Sent)
                Sent.Add(requestAsJson);

            var peer = _peer;
            _ = Task.Run(() =>
            {
                lock (peer.Received)
                    peer.Received.Add(requestAsJson);
                peer.OnMessageReceived(requestAsJson);
            });
            return Task.CompletedTask;
        }
    }
}
