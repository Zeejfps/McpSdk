using System;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Server;
using McpSdk.Shared;

namespace McpSdk.Adapter.StreamableHttpServer
{
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

        public Task Log(LoggingLevel level, Json data, string logger = null) =>
            throw new NotSupportedException(
                "Logging is per-session on the Streamable HTTP host; obtain the per-session server to log " +
                "(implementation-plan decision #6).");

        private async Task<IServer> OnSession(ITransport transport)
        {
            var childContext = new DiContainer();

            childContext.AddSingleton<ITransport>(transport);

            var httpTransport = transport as HttpServerTransport;
            var sessionId = httpTransport?.SessionId;
            var origin = httpTransport?.Origin;
            var session = new Session(childContext, sessionId, origin, transport);

            _options.SessionConfigurator?.Invoke(session);

            childContext.AddServerSession();

            var childProvider = childContext.BuildServiceProvider(_rootProvider);

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
