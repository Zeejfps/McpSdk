using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Client
{
    internal sealed class SseTransportFactory : ITransportFactory
    {
        private readonly IJson _json;
        private readonly ISseClientFactory _sseClientFactory;

        public SseTransportFactory(IJson json, ISseClientFactory sseClientFactory)
        {
            _json = json;
            _sseClientFactory = sseClientFactory;
        }

        public ITransport Create(ILoggerFactory loggerFactory)
        {
            var sseClient = _sseClientFactory.Create();
            return new SseTransport(sseClient, _json, loggerFactory);
        }
    }
}