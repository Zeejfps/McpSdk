using McpSdk.Client;
using McpSdk.Shared;

namespace McpSdk.Adapter.SseClient
{
    public class SseClientFactory : ISseClientFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        public SseClientFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public ISseClient Create()
        {
            return new SseClient(_loggerFactory);
        }
    }
}