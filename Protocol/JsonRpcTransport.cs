using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace McpSdk.Protocol
{
    public abstract class JsonRpcTransport : ITransport
    {
        private const string JsonRpcVersion = "2.0";

        private readonly IJson _json;
        private readonly Dictionary<int, TaskCompletionSource<IJsonObject>> _tscByMessageId = new Dictionary<int, TaskCompletionSource<IJsonObject>>();
        
        private int _nextMessageId;

        protected JsonRpcTransport(IJson json)
        {
            _json = json;
        }
        
        public event RequestReceivedCallback RequestReceived;
        public event NotificationReceivedCallback NotificationReceived;
        
        public Task Start(CancellationToken cancellationToken = default)
        {
            return OnStart(cancellationToken);
        }

        public async Task SendNotification(string notification, CancellationToken cancellationToken = default)
        {
            var requestAsJson = _json.Stringify(request =>
            {
                request.Write("jsonrpc", JsonRpcVersion);
                request.Write("method", notification);
            });
            await Send(requestAsJson, cancellationToken);
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

            await Send(request, cancellationToken);
            var response = await WaitForResponse(id, cancellationToken);
            return ReadResult(response);
        }

        public async Task SendOkResponse(int requestId, Action<IJsonWriter> payload, CancellationToken cancellationToken = default)
        {
            var response = _json.Stringify(req =>
            {
                req.Write("jsonrpc", JsonRpcVersion);
                req.Write("id", requestId);
                req.Write("result", payload);
            });
            await Send(response, cancellationToken);
        }
        
        public async Task SendErrorResponse(int requestId, Action<IJsonWriter> error, CancellationToken cancellationToken = default)
        {
            var response = _json.Stringify(req =>
            {
                req.Write("jsonrpc", JsonRpcVersion);
                req.Write("id", requestId);
                req.Write("error", error);
            });
            await Send(response, cancellationToken);
        }
        
        protected void OnMessageReceived(string messageAsJson)
        {
            Console.WriteLine($"Received: {messageAsJson}");

            var response = _json.Parse(messageAsJson);
            var idProp = response["id"];
            var method = response["method"]?.AsString();

            if (method != null)
            {
                if (idProp == null)
                {
                    NotificationReceived?.Invoke(method);
                }
                else
                {
                    var id = idProp.AsInt();
                    var methodParams = response["params"]?.AsObject();
                    RequestReceived?.Invoke(id, method, methodParams);
                }
            }
            else
            {
                if (idProp == null)
                {
                    return;
                }
                
                var id = idProp.AsInt();
                if (!_tscByMessageId.TryGetValue(id, out var tsc))
                    return;
                
                _tscByMessageId.Remove(id);
                tsc.TrySetResult(response);
            }
        }
        
        protected abstract Task OnStart(CancellationToken cancellationToken = default);
        protected abstract Task Send(string requestAsJson, CancellationToken cancellationToken);

        private Task<IJsonObject> WaitForResponse(int messageId, CancellationToken cancellationToken = default)
        {
            var tsc = new TaskCompletionSource<IJsonObject>(cancellationToken);
            _tscByMessageId[messageId] = tsc;
            return tsc.Task;
        }
        
        private IJsonObject ReadResult(IJsonObject response)
        {
            var errorProp = response["error"];
            if (errorProp != null)
            {
                var errorObj = errorProp.AsObject();
                var code = errorObj["code"].AsInt();
                var message = errorObj["message"].AsString();
                throw new Exception($"Error ({code}): {message}");
            }
            return response["result"].AsObject();
        }
        
        private int NextRequestId()
        {
            return Interlocked.Increment(ref _nextMessageId);
        }
    }
}