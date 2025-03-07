using System.Threading;
using System.Threading.Tasks;

namespace McpSharp.Client
{
    internal sealed class HttpConnectionFactory : IConnectionFactory
    {
        private readonly IJson _json;
        private readonly IHttpClientFactory _httpClientFactory;

        public HttpConnectionFactory(IJson json, IHttpClientFactory httpClientFactory)
        {
            _json = json;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IConnection> CreateConnection(CancellationToken cancellationToken = default)
        {
            var httpClient = await _httpClientFactory.CreateHttpClient("http://localhost:3000/sse");
            return new HttpConnection(httpClient, _json, "test");
        }
    }
}