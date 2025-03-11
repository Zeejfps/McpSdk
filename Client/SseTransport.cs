using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using McpSharp.Protocol;

namespace McpSharp.Client
{
    internal sealed class SseTransport : ITransport
    {
        private const string JsonRpcVersion = "2.0";
        
        private readonly IJson _json;
        private readonly ISseClient _sseClient;
        private readonly string _host;
        private readonly string _connectionUrl;
        private readonly Dictionary<int, TaskCompletionSource<IJsonObject>> _tscByMessageId = new Dictionary<int, TaskCompletionSource<IJsonObject>>();

        private int _nextMessageId;
        private string _messagesUrl;

        public SseTransport(ISseClient sseClient, IJson json, string host)
        {
            _json = json;
            _sseClient = sseClient;
            _host = host;
            _connectionUrl = $"{host}/sse";
            _messagesUrl = $"{host}/messages";
        }

        public event RequestReceivedCallback RequestReceived;
        public event NotificationReceivedCallback NotificationReceived;

        public async Task Connect(CancellationToken cancellationToken = default)
        {
            _sseClient.EventReceived += OnSseEventReceived;
            await _sseClient.Connect(_connectionUrl, cancellationToken);
        }

        private void OnSseEventReceived(ISseEvent sseEvent)
        {
            if (sseEvent.Kind == "endpoint")
            {
                _messagesUrl = $"{_host}{sseEvent.Data}";
            }
            else if (sseEvent.Kind == "message")
            {
                OnMessageReceived(sseEvent.Data);
            }
        }

        private void OnMessageReceived(string message)
        {
            var response = _json.Parse(message);
            var idProp = response["id"];
            if (idProp == null) 
                return;
            
            var id = idProp.AsInt();
            if (!_tscByMessageId.TryGetValue(id, out var tsc))
                return;
            
            _tscByMessageId.Remove(id);
            tsc.TrySetResult(response);
        }
        
        private Task<IJsonObject> WaitForResponse(int messageId, CancellationToken cancellationToken = default)
        {
            var tsc = new TaskCompletionSource<IJsonObject>(cancellationToken);
            _tscByMessageId[messageId] = tsc;
            return tsc.Task;
        }
        
        public async Task SendNotification(string notification, CancellationToken cancellationToken = default)
        {
            var request = _json.Stringify(req =>
            {
                req.Write("jsonrpc", JsonRpcVersion);
                req.Write("method", notification);
            });
            await _sseClient.SendMessage(_messagesUrl, request, cancellationToken);
        }

        public async Task<IJsonObject> SendRequest(string method, Action<IJsonWriter> payload, CancellationToken cancellationToken = default)
        {
            var id = NextRequestId();
            var request = _json.Stringify(req =>
            {
                req.Write("jsonrpc", JsonRpcVersion);
                req.Write("id", id);
                req.Write("method", method);
                req.Write("params", payload);
            });
            await _sseClient.SendMessage(_messagesUrl, request, cancellationToken);
            var response = await WaitForResponse(id, cancellationToken).ConfigureAwait(false);
            return ReadResult(response);
        }

        public async Task SendResponse(int messageId, string method, Action<IJsonWriter> payload, CancellationToken cancellationToken = default)
        {
            var response = _json.Stringify(req =>
            {
                req.Write("jsonrpc", JsonRpcVersion);
                req.Write("id", messageId);
                req.Write("result", payload);
            });
            await _sseClient.SendMessage(_messagesUrl, response, cancellationToken);
        }

        private IJsonObject ReadResult(IJsonObject response)
        {
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
            return Interlocked.Increment(ref _nextMessageId);
        }
    }
}