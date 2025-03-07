namespace McpSharp.Client
{
    internal sealed class SseTransportFactory : ITransportFactory
    {
        private readonly IJson _json;
        private readonly IHttpClientFactory _httpClientFactory;

        public SseTransportFactory(IJson json, IHttpClientFactory httpClientFactory)
        {
            _json = json;
            _httpClientFactory = httpClientFactory;
        }

        public ITransport Create()
        {
            var httpClient = _httpClientFactory.Create("http://localhost:3000/sse");
            return new SseTransport(httpClient, _json, "test");
        }
    }
}