namespace McpSharp.Client
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

        public ITransport Create()
        {
            var httpClient = _sseClientFactory.Create("http://localhost:3000/sse");
            return new SseTransport(httpClient, _json, "test");
        }
    }
}