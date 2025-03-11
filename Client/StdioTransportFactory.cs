using McpSharp.Protocol;

namespace McpSharp.Client
{
    internal sealed class StdioTransportFactory : ITransportFactory
    {
        public ITransport Create()
        {
            return new StdioTransport();
        }
    }
}