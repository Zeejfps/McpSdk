using McpSdk.Protocol;

namespace McpSdk.Server
{
    internal sealed class StdioTransportFactory : ITransportFactory
    {
        private readonly IJson _json;

        public StdioTransportFactory(IJson json)
        {
            _json = json;
        }

        public ITransport Create()
        {
            return new StdioTransport(_json);
        }
    }
}