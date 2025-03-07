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

        public Task<IConnection> CreateConnection(CancellationToken cancellationToken = default)
        {
            var httpClient = _httpClientFactory.CreateHttpClient();
            var connection = new HttpConnection(httpClient, _json);
            return Task.FromResult<IConnection>(connection);
        }
    }
}