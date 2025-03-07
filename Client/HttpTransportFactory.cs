namespace McpSharp.Client
{
    internal sealed class HttpTransportFactory : ITransportFactory
    {
        private readonly IJson _json;
        private readonly IHttpClientFactory _httpClientFactory;

        public HttpTransportFactory(IJson json, IHttpClientFactory httpClientFactory)
        {
            _json = json;
            _httpClientFactory = httpClientFactory;
        }

        public ITransport Create()
        {
            var httpClient = _httpClientFactory.CreateHttpClient("http://localhost:3000/sse");
            return new HttpConnection(httpClient, _json, "test");
        }
    }
}