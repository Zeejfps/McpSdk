using McpSdk.Client;
using McpSdk.Shared;

namespace McpSdk.Adapter.SseClient
{
    public class SseClientFactory : ISseClientFactory
    {
        private readonly string _baseUrl;
        private readonly string _connectionEndpoint;
        private readonly ILoggerFactory _loggerFactory;

        public SseClientFactory(string baseUrl, string connectionEndpoint, ILoggerFactory loggerFactory)
        {
            _baseUrl = baseUrl;
            _connectionEndpoint = connectionEndpoint;
            _loggerFactory = loggerFactory;
        }

        public ISseClient Create()
        {
            return new SseClient(_baseUrl, _connectionEndpoint, _loggerFactory);
        }
    }
}