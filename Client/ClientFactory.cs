using McpSharp.Protocol;

namespace McpSharp.Client
{
    public sealed class ClientFactory
    {
        private readonly ITransportFactory _transportFactory;
        
        public ClientFactory(IJson json, ISseClientFactory sseClientFactory)
        {
            _transportFactory = new SseTransportFactory(json, sseClientFactory);
        }
        
        public IClient CreateClient(ClientInfo clientInfo)
        {
            var connection = _transportFactory.Create();
            return new McpClient(connection, clientInfo);
        }
    }
}