using System;
using System.Threading;
using System.Threading.Tasks;
using McpSharp.Protocol;
using McpSharp.Protocol.Messages;

namespace McpSharp.Client
{
    internal sealed class SseTransport : ITransport
    {
        private readonly string _endpoint;
        private readonly IJson _json;
        private readonly ISseClient _sseClient;
        private readonly string _url;

        public SseTransport(ISseClient sseClient, IJson json, string endpoint)
        {
            _json = json;
            _endpoint = endpoint;
            _sseClient = sseClient;
            _url = "http://localhost:3000/messages";
        }

        public async Task Connect()
        {
            await _sseClient.Connect("http://localhost:3000/sse");
            Console.WriteLine($"Connected");
        }

        public async Task<InitializeResponseMessage> SendMessage(InitializeMessage message, CancellationToken cancellationToken = default)
        {
            var request = new JsonRpcRequest<int, InitializeMessage>(1, "initialize", message);
            var requestAsJson = _json.Stringify(request);
            
            await _sseClient.PostMessage(_url, requestAsJson, cancellationToken);

            ISseMessage sseMessage;
            do
            {
                sseMessage = await _sseClient.DequeueMessage(cancellationToken);
            } while(sseMessage.Kind != "message");
            
            Console.WriteLine($"SSE Message: {sseMessage}");

            var responsePayloadAsJson = sseMessage.Data;
            _json.Parse(responsePayloadAsJson, out JsonRpcResponse<int, InitializeResponseMessage> jsonRpcResponse);
            if (jsonRpcResponse.Error != null)
                throw new ClientException(jsonRpcResponse.Error.ToString());
            return jsonRpcResponse.Result;
        }

        public async Task SendNotification(InitializedNotification notification, CancellationToken cancellationToken = default)
        {
            var request = new JsonRpcNotification("initialized");
            var requestAsJson = _json.Stringify(request);
            await _sseClient.PostMessage(_url, requestAsJson, cancellationToken);
        }

        public void Dispose()
        {
            _sseClient?.Dispose();
        }
    }
}