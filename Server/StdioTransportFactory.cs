using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Server
{
    public sealed class StdioTransportFactory : ITransportFactory
    {
        private readonly IJson _json;
        private readonly ILoggerFactory _loggerFactory;

        public StdioTransportFactory(IJson json, ILoggerFactory loggerFactory)
        {
            _json = json;
            _loggerFactory = loggerFactory;
        }

        public ITransport Create()
        {
            return new StdioTransport(_json, _loggerFactory);
        }
    }
}