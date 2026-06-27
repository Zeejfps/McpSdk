using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>A transport factory that always hands back a pre-built transport instance.</summary>
    public sealed class FixedTransportFactory : ITransportFactory
    {
        private readonly ITransport _transport;

        public FixedTransportFactory(ITransport transport)
        {
            _transport = transport;
        }

        public ITransport Create(ILoggerFactory loggerFactory) => _transport;
    }
}
