using McpSharp.Protocol;

namespace McpSharp.Client
{
    public sealed class ClientFactory
    {
        private readonly IConnectionFactory _connectionFactory;
        
        public ClientFactory(IJson json, IHttpClientFactory httpClientFactory)
        {
            _connectionFactory = new HttpConnectionFactory(json, httpClientFactory);
        }
        
        public IClient CreateClient(ClientInfo clientInfo)
        {
            return new McpClient(_connectionFactory, clientInfo);
        }
    }
}