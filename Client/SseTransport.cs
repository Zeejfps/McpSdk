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

        private int _nextRequestId;
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

        public async Task<InitializeResultPayload> SendMessage(InitializeRequestPayload payload, CancellationToken cancellationToken = default)
        {
            var request = CreateRequest("initialize", payload);
            var requestAsJson = _json.Stringify(request);
            
            await _sseClient.SendMessage(_messagesUrl, requestAsJson, cancellationToken);

            var endpointMessage = await _sseClient.DequeueEvent(cancellationToken);
            if (endpointMessage.Kind != "endpoint")
                throw new Exception($"Expected endpoint message, got: {endpointMessage.Kind}");

            _messagesUrl = $"{_host}/{endpointMessage.Data}";
            
            var initializeResponseMessage = await _sseClient.DequeueEvent(cancellationToken);
            if (initializeResponseMessage.Kind != "message")
                throw new Exception($"Expected message, got: {initializeResponseMessage.Kind}");
            
            var responsePayloadAsJson = initializeResponseMessage.Data;
            _json.Parse(responsePayloadAsJson, out JsonRpcResponse<int, InitializeResultPayload> jsonRpcResponse);
            if (jsonRpcResponse.Error != null)
                throw new ClientException(jsonRpcResponse.Error.ToString());
            
            return jsonRpcResponse.Result;
        }

        public async Task<ListToolsResultPayload> SendMessage(ListToolsRequestPayload payload, CancellationToken cancellationToken = default)
        {
            var request = CreateRequest("tools/list", payload);
            var requestAsJson = _json.Stringify(request);
            
            await _sseClient.SendMessage(_messagesUrl, requestAsJson, cancellationToken);
            var sseEvent = await _sseClient.DequeueEvent(cancellationToken);
            if (sseEvent.Kind != "message")
                throw new Exception($"Expected event kind: message, got: {sseEvent.Kind}");
            
            var responsePayloadAsJson = sseEvent.Data;
            _json.Parse(responsePayloadAsJson, out JsonRpcResponse<int, ListToolsResultPayload> jsonRpcResponse);
            if (jsonRpcResponse.Error != null)
                throw new ClientException(jsonRpcResponse.Error.ToString());

            return jsonRpcResponse.Result;
        }

        public async Task<CallToolResultPayload> SendMessage(CallToolRequestPayload payload, CancellationToken cancellationToken = default)
        {
            var request = CreateRequest("tools/call", payload);
            var requestAsJson = _json.Stringify(request);
            
            await _sseClient.SendMessage(_messagesUrl, requestAsJson, cancellationToken);
            var sseEvent = await _sseClient.DequeueEvent(cancellationToken);
            if (sseEvent.Kind != "message")
                throw new Exception($"Expected event kind: message, got: {sseEvent.Kind}");
            
            var responsePayloadAsJson = sseEvent.Data;
            _json.Parse(responsePayloadAsJson, out JsonRpcResponse<int, CallToolResultPayload> jsonRpcResponse);
            if (jsonRpcResponse.Error != null)
                throw new ClientException(jsonRpcResponse.Error.ToString());

            return jsonRpcResponse.Result;
        }

        public async Task SendNotification(InitializedNotification notification, CancellationToken cancellationToken = default)
        {
            var request = new JsonRpcNotification("initialized");
            var requestAsJson = _json.Stringify(request);
            await _sseClient.SendMessage(_messagesUrl, requestAsJson, cancellationToken);
        }

        private int NextRequestId()
        {
            return Interlocked.Increment(ref _nextRequestId);
        }

        private JsonRpcRequest<int, TPayload> CreateRequest<TPayload>(string method, TPayload payload)
        {
            var requestId = NextRequestId();
            return new JsonRpcRequest<int, TPayload>(requestId, method, payload);
        }
    }
}