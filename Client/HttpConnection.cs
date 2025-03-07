using System.Threading;
using System.Threading.Tasks;
using McpSharp.Protocol;
using McpSharp.Protocol.Messages;

namespace McpSharp.Client
{
    internal sealed class HttpConnection : IConnection
    {
        private readonly IJson _json;
        private readonly IHttpClient _httpClient;

        public HttpConnection(IHttpClient httpClient, IJson json)
        {
            _json = json;
            _httpClient = httpClient;
        }

        public async Task<InitializeResponseMessage> SendMessage(InitializeMessage message, CancellationToken cancellationToken = default)
        {
            var request = new JsonRpcRequest<int, InitializeMessage>(1, "initialize", message);
            var requestAsJson = _json.Stringify(request);
            var endpoint = "asdf";
            var response = await _httpClient.Post<InitializeResponseMessage>(endpoint, requestAsJson, cancellationToken);
            if (response.Error != null)
                throw new ClientException(response.Error.ToString());
            return response.Result;
        }

        public async Task SendNotification(InitializedNotification notification, CancellationToken cancellationToken = default)
        {
            var request = new JsonRpcNotification("initialized");
            var requestAsJson = _json.Stringify(request);
            var endpoint = "asdf";
            await _httpClient.Post(endpoint, requestAsJson, cancellationToken);
        }
    }
}