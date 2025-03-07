using McpSharp.Protocol;

namespace McpSharp.Client
{
    public sealed class ClientFactory
    {
        private readonly ITransportFactory _transportFactory;
        
        public ClientFactory(IJson json, IHttpClientFactory httpClientFactory)
        {
            _transportFactory = new HttpTransportFactory(json, httpClientFactory);
        }
        
        public IClient CreateClient(ClientInfo clientInfo)
        {
            var connection = _transportFactory.Create();
            return new McpClient(connection, clientInfo);
        }
    }
}