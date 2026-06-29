using System;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Shared;

namespace McpSdk.Server
{
    internal sealed class SingleSessionServerHost : IServerHost
    {
        private readonly IServiceProvider _provider;
        private McpServer _session;

        public SingleSessionServerHost(IServiceProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private McpServer Session => _session ??= _provider.GetService<McpServer>()
            ?? throw new InvalidOperationException(
                "No session server is registered; the transport registration must also register the session via AddServerSession().");

        public Task Start() => Session.Start();

        public Task Stop() => _session == null ? Task.CompletedTask : _session.Stop();

        public Task Log(LoggingLevel level, Json data, string logger = null) => Session.Log(level, data, logger);
    }
}
