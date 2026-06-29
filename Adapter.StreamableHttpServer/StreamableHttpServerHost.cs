using System;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Server;
using McpSdk.Shared;

namespace McpSdk.Adapter.StreamableHttpServer
{
    /// <summary>
    /// The multi-session <see cref="IServerHost"/> for the Streamable HTTP transport. It owns the
    /// <see cref="StreamableHttpListener"/> and, for each connection the listener accepts, builds a fresh
    /// <b>per-session child scope</b> over the host's root provider and runs one <c>McpServer</c> in it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registered as the single <see cref="IServerHost"/> by
    /// <see cref="StreamableHttpServerTransportExtensions.AddStreamableHttpTransport"/> with a
    /// <c>sp =&gt; new StreamableHttpServerHost(sp, ...)</c> factory, so the <paramref name="rootProvider"/> it
    /// captures is the <b>root</b> provider <see cref="ServerBuilder.Build"/> built. Each connection's transport
    /// is different, so — unlike the single-session stdio/in-memory hosts which register one transport in the
    /// root — it cannot be a root singleton; it is registered into a per-connection child scope, from which the
    /// session's <c>McpServer</c> resolves it (implementation-plan T9 grounding note, T4 child scope).
    /// </para>
    /// <para>
    /// <b>Logging (decision #6).</b> <see cref="Log"/> throws <see cref="NotSupportedException"/>: this host has
    /// no single session, and <c>logging</c> is advertised + served per session, so there is no host-level log
    /// channel (unlike the single-session host, which forwards to its one session).
    /// </para>
    /// </remarks>
    internal sealed class StreamableHttpServerHost : IServerHost
    {
        private readonly IServiceProvider _rootProvider;
        private readonly string _baseUrl;
        private readonly string _path;
        private readonly StreamableHttpServerOptions _options;

        private StreamableHttpListener _listener;

        public StreamableHttpServerHost(
            IServiceProvider rootProvider,
            string baseUrl,
            string path,
            StreamableHttpServerOptions options)
        {
            _rootProvider = rootProvider ?? throw new ArgumentNullException(nameof(rootProvider));
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public Task Start()
        {
            // Build the listener from the root scope's serializer + logger; the allowed-origins set is null
            // when none were configured (listener reads that as "origin checking disabled").
            _listener = new StreamableHttpListener(
                _baseUrl,
                _path,
                _rootProvider.GetService<IJson>(),
                _rootProvider.GetService<ILoggerFactory>(),
                OnSession,
                _options.AllowedOrigins);
            return _listener.Start();
        }

        public Task Stop() => _listener == null ? Task.CompletedTask : _listener.Stop();

        /// <summary>
        /// Not supported on the multi-session HTTP host (implementation-plan decision #6): logging is
        /// advertised and served per session, so there is no host-level log channel.
        /// </summary>
        public Task Log(LoggingLevel level, Json data, string logger = null) =>
            throw new NotSupportedException(
                "Logging is per-session on the Streamable HTTP host; obtain the per-session server to log " +
                "(implementation-plan decision #6).");

        private async Task<IServer> OnSession(ITransport transport)
        {
            // (1) A fresh child container — this becomes the per-session session.Context.
            var childContext = new DiContainer();

            // (2) Register this connection's transport into the child so the McpServer factory (resolved from
            //     the child below) picks up the per-connection transport rather than a root singleton.
            childContext.AddSingleton<ITransport>(transport);

            // Construct the session over the child context. SessionId and Origin come from the listener's
            // per-connection HttpServerTransport — Origin is the connection's HTTP Origin header (or null).
            var httpTransport = transport as HttpServerTransport;
            var sessionId = httpTransport?.SessionId;
            var origin = httpTransport?.Origin;
            var session = new Session(childContext, sessionId, origin, transport);

            // === T13b: per-session ConfigureSession + tool aggregation ===
            // Invoke the stored configurator HERE — against session.Context (the child), AFTER the connection's
            // ITransport is registered above and BEFORE the child provider is built below. Per-session
            // registrations (e.g. session.Context.AddToolsCapability(...)) therefore land in the child container
            // and aggregate with the root's via the composite's parent-then-child GetServices overlay
            // (root ∪ session). Sibling sessions are isolated because each gets its own child container.
            _options.SessionConfigurator?.Invoke(session);

            // (3) Register the session McpServer factory in the child (pulls the child's ITransport + the root's
            //     ServerInfo/logger/controllers via the child→parent delegation + GetServices overlay).
            childContext.AddServerSession();

            // (4) Freeze the child as a scope layered over the root.
            var childProvider = childContext.BuildServiceProvider(_rootProvider);

            // (5) Resolve the McpServer from the CHILD provider and start it.
            var server = childProvider.GetRequiredService<McpServer>();
            await server.Start();
            return new SessionScope(server, childProvider as IDisposable);
        }

        private sealed class SessionScope : IServer
        {
            private readonly IServer _server;
            private readonly IDisposable _scope;

            public SessionScope(IServer server, IDisposable scope)
            {
                _server = server;
                _scope = scope;
            }

            public Task Start() => _server.Start();

            public async Task Stop()
            {
                try
                {
                    await _server.Stop();
                }
                finally
                {
                    _scope?.Dispose();
                }
            }

            public Task Log(LoggingLevel level, Json data, string logger = null) => _server.Log(level, data, logger);
        }
    }
}
