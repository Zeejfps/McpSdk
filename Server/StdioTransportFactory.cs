using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Server
{
    public sealed class StdioTransportFactory : ITransportFactory
    {
        private readonly IJson _json;

        public StdioTransportFactory(IJson json)
        {
            _json = json;
        }

        public ITransport Create(ILoggerFactory loggerFactory)
        {
            return new StdioTransport(_json, loggerFactory);
        }
    }
}