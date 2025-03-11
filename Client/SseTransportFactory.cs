using McpSharp.Protocol;

namespace McpSharp.Client
{
    internal sealed class SseTransportFactory : ITransportFactory
    {
        private readonly IJson _json;
        private readonly ISseClientFactory _sseClientFactory;
        private readonly string _host;

        public SseTransportFactory(IJson json, ISseClientFactory sseClientFactory, string host)
        {
            _json = json;
            _sseClientFactory = sseClientFactory;
            _host = host;
        }

        public ITransport Create()
        {
            var sseClient = _sseClientFactory.Create();
            return new SseTransport(sseClient, _json, _host);
        }
    }
}