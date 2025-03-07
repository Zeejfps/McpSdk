using System.Threading;
using System.Threading.Tasks;
using McpSharp.Protocol;
using McpSharp.Protocol.Messages;

namespace McpSharp.Client
{
    internal sealed class HttpConnection : IConnection
    {
        private readonly string _endpoint;
        private readonly IJson _json;
        private readonly IHttpClient _httpClient;

        public HttpConnection(IHttpClient httpClient, IJson json, string endpoint)
        {
            _json = json;
            _endpoint = endpoint;
            _httpClient = httpClient;
        }

        public async Task<InitializeResponseMessage> SendMessage(InitializeMessage message, CancellationToken cancellationToken = default)
        {
            var request = new JsonRpcRequest<int, InitializeMessage>(1, "initialize", message);
            var requestAsJson = _json.Stringify(request);
            var endpoint = "messages";
            var response = await _httpClient.Post<InitializeResponseMessage>(endpoint, requestAsJson, cancellationToken);
            var responsePayloadAsJson = await response.ReadContentAsJsonString();
            _json.Parse(responsePayloadAsJson, out JsonRpcResponse<int, InitializeResponseMessage> jsonRpcResponse);
            if (jsonRpcResponse.Error != null)
                throw new ClientException(jsonRpcResponse.Error.ToString());
            return jsonRpcResponse.Result;
        }

        public async Task SendNotification(InitializedNotification notification, CancellationToken cancellationToken = default)
        {
            var request = new JsonRpcNotification("initialized");
            var requestAsJson = _json.Stringify(request);
            var endpoint = "messages";
            await _httpClient.Post(endpoint, requestAsJson, cancellationToken);
        }
    }
}