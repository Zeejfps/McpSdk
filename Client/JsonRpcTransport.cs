using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using McpSharp.Protocol;

namespace McpSharp.Client
{
    internal abstract class JsonRpcTransport : ITransport
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
        
        public Task Connect(CancellationToken cancellationToken = default)
        {
            return OnConnect(cancellationToken);
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

        public async Task SendResponse(int messageId, Action<IJsonWriter> payload, CancellationToken cancellationToken = default)
        {
            var response = _json.Stringify(req =>
            {
                req.Write("jsonrpc", JsonRpcVersion);
                req.Write("id", messageId);
                req.Write("result", payload);
            });
            await Send(response, cancellationToken);
        }
        
        protected void OnResponseReceived(string responseAsJson)
        {
            Console.WriteLine($"Received: {responseAsJson}");

            var response = _json.Parse(responseAsJson);
            var idProp = response["id"];
            if (idProp == null) 
                return;
            
            var id = idProp.AsInt();
            if (!_tscByMessageId.TryGetValue(id, out var tsc))
                return;
            
            _tscByMessageId.Remove(id);
            tsc.TrySetResult(response);
        }
        
        protected abstract Task OnConnect(CancellationToken cancellationToken = default);
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