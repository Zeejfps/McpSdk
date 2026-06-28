using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Client
{
    public sealed class SseTransportFactory : ITransportFactory
    {
        private readonly IJson _json;
        private readonly ISseClientFactory _sseClientFactory;
        private readonly ILoggerFactory _loggerFactory;

        public SseTransportFactory(IJson json, ISseClientFactory sseClientFactory, ILoggerFactory loggerFactory)
        {
            _json = json;
            _sseClientFactory = sseClientFactory;
            _loggerFactory = loggerFactory;
        }

        public ITransport Create()
        {
            var sseClient = _sseClientFactory.Create();
            return new SseTransport(sseClient, _json, _loggerFactory);
        }
    }
}