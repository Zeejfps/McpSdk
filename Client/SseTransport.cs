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
            var sseEvent = await _sseClient.DequeueEvent();
            if (sseEvent.Kind != "endpoint")
                throw new Exception($"Expected endpoint event, received: {sseEvent.Kind}");
            var messagesEndpoint = sseEvent.Data;
            if (!messagesEndpoint.StartsWith("/"))
                messagesEndpoint = $"/{messagesEndpoint}";
            _messagesUrl = $"{_host}{messagesEndpoint}";
        }

        public async Task SendNotification(InitializedNotification notification, CancellationToken cancellationToken = default)
        {
            var request = new JsonRpcNotification("initialized");
            var requestAsJson = _json.Stringify(request);
            await _sseClient.SendMessage(_messagesUrl, requestAsJson, cancellationToken);
        }

        public async Task<IJsonObject> SendMessage(string method, Action<IJsonWriter> payload, CancellationToken cancellationToken = default)
        {
            var jsonRpcRequest = WriteJsonRpcRequest(method, payload);
            await _sseClient.SendMessage(_messagesUrl, jsonRpcRequest, cancellationToken);
            var sseEvent = await _sseClient.DequeueEvent(cancellationToken);
            return ReadResult(sseEvent.Data);
        }

        private string WriteJsonRpcRequest(string method, Action<IJsonWriter> payload)
        {
            var id = NextRequestId();
            return _json.Stringify(writer =>
            {
                writer.Write("jsonrpc", "2.0");
                writer.Write("id", id);
                writer.Write("method", method);
                writer.Write("params", payload);
            });
        }

        private IJsonObject ReadResult(string text)
        {
            var response = _json.Parse(text);
            var errorProp = response["error"];
            if (errorProp != null)
            {
                var errorObj = errorProp.AsObject();
                var code = errorObj["code"].AsInt();
                var message = errorObj["message"].AsString();
                throw new ClientException($"Error ({code}): {message}");
            }
            return response["result"].AsObject();
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