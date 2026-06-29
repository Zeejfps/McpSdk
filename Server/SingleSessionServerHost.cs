using System;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Shared;

namespace McpSdk.Server
{
    /// <summary>
    /// Single-session <see cref="IServerHost"/> shared by every transport that owns exactly one
    /// <see cref="McpServer"/> session over a single <see cref="ITransport"/> (stdio via
    /// <see cref="StdioServerTransportExtensions.AddStdioTransport"/>, and the in-memory test transport via
    /// <see cref="InMemoryServerTransportExtensions.AddInMemoryServerTransport"/>). It owns the lifecycle of
    /// that one session, which it resolves from the container the transport registration built — the session
    /// in turn pulls the singleton <see cref="ITransport"/> from that same root scope (implementation-plan T9
    /// grounding note: the single-session transports register one transport in the root). The host is
    /// transport-agnostic: stdio <i>creates</i> its transport, the in-memory host <i>receives</i> a pre-built
    /// one, but both register this same host because it only ever resolves <see cref="ITransport"/> +
    /// <see cref="McpServer"/> from the provider.
    /// </summary>
    /// <remarks>
    /// The host takes an <see cref="IServiceProvider"/> rather than the session directly: the provider
    /// resolves itself, so the transport registration can register the host with a
    /// <c>sp =&gt; new SingleSessionServerHost(sp)</c> factory and the host defers resolving the
    /// <see cref="McpServer"/> until it is started. Because <c>AddServerSession</c> registers the session as
    /// a singleton, every resolve returns the one shared instance.
    /// </remarks>
    internal sealed class SingleSessionServerHost : IServerHost
    {
        private readonly IServiceProvider _provider;
        private McpServer _session;

        public SingleSessionServerHost(IServiceProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>
        /// The single session server, resolved (once) from the container. <c>AddServerSession</c> registered
        /// it as a singleton, so this returns the shared instance; it pulls the singleton
        /// <see cref="ITransport"/> registered alongside it.
        /// </summary>
        private McpServer Session => _session ??= _provider.GetService<McpServer>()
            ?? throw new InvalidOperationException(
                "No session server is registered; the transport registration must also register the session via AddServerSession().");

        public Task Start() => Session.Start();

        public Task Stop() => _session == null ? Task.CompletedTask : _session.Stop();

        /// <summary>
        /// Delegates to the single session (implementation-plan decision #6: a single-session host owns exactly
        /// one session, so its <see cref="IServer.Log"/> forwards there — unlike the multi-session HTTP host).
        /// </summary>
        public Task Log(LoggingLevel level, Json data, string logger = null) => Session.Log(level, data, logger);
    }
}
