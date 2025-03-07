using System;
using System.Threading;
using System.Threading.Tasks;
using McpSharp.Protocol;
using McpSharp.Protocol.Messages;

namespace McpSharp.Client
{
    internal sealed class SseTransport : ITransport
    {
        private readonly IJson _json;
        private readonly ISseClient _sseClient;
        private readonly string _host;
        private readonly string _connectionUrl;
        
        private string _messagesUrl;

        public SseTransport(ISseClient sseClient, IJson json, string host)
        {
            _json = json;
            _sseClient = sseClient;
            _host = host;
            _connectionUrl = $"{host}/sse";
            _messagesUrl = $"{host}/messages";
        }

        public async Task Connect()
        {
            await _sseClient.Connect(_connectionUrl);
        }

        public async Task<InitializeResponseMessage> SendMessage(InitializeMessage message, CancellationToken cancellationToken = default)
        {
            var request = new JsonRpcRequest<int, InitializeMessage>(1, "initialize", message);
            var requestAsJson = _json.Stringify(request);
            
            await _sseClient.SendMessage(_messagesUrl, requestAsJson, cancellationToken);

            var endpointMessage = await _sseClient.DequeueMessage(cancellationToken);
            if (endpointMessage.Kind != "endpoint")
                throw new Exception($"Expected endpoint message, got: {endpointMessage.Kind}");

            _messagesUrl = $"{_host}{endpointMessage.Data}";
            
            var initializeResponseMessage = await _sseClient.DequeueMessage(cancellationToken);
            if (initializeResponseMessage.Kind != "message")
                throw new Exception($"Expected message, got: {initializeResponseMessage.Kind}");
            
            var responsePayloadAsJson = initializeResponseMessage.Data;
            _json.Parse(responsePayloadAsJson, out JsonRpcResponse<int, InitializeResponseMessage> jsonRpcResponse);
            if (jsonRpcResponse.Error != null)
                throw new ClientException(jsonRpcResponse.Error.ToString());
            
            return jsonRpcResponse.Result;
        }

        public Task<ListToolsResult> SendMessage(ListToolsRequest payload, CancellationToken cancellationToken = default)
        {
            var request = new JsonRpcRequest<int, ListToolsRequest>(1, "tools/list", payload);
            var requestAsJson = _json.Stringify(request);

            throw new NotImplementedException();
        }

        public async Task SendNotification(InitializedNotification notification, CancellationToken cancellationToken = default)
        {
            var request = new JsonRpcNotification("initialized");
            var requestAsJson = _json.Stringify(request);
            await _sseClient.SendMessage(_messagesUrl, requestAsJson, cancellationToken);
        }
        
    }
}