using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;

namespace McpSdk.Shared
{
    public abstract class JsonRpcTransport : ITransport
    {
        private const string JsonRpcVersion = "2.0";

        private readonly IJson _json;
        private readonly Dictionary<int, TaskCompletionSource<IJsonObject>> _tscByMessageId = new();
        
        private int _nextMessageId;
        
        protected ILogger Logger { get; }

        protected JsonRpcTransport(IJson json, ILoggerFactory loggerFactory)
        {
            _json = json;
            Logger = loggerFactory.Create(GetType());
        }
        
        public event RequestReceivedCallback RequestReceived;
        public event NotificationReceivedCallback NotificationReceived;
        
        public Task Start(CancellationToken cancellationToken = default)
        {
            return OnStart(cancellationToken);
        }

        public Task Stop()
        {
            Logger.LogDebug("Stopping transport...");
            return OnStop();
        }

        public async Task SendNotification(string notification, Json arguments = null, CancellationToken cancellationToken = default)
        {
            var requestAsJson = _json.Stringify(request =>
            {
                request.Write("jsonrpc", JsonRpcVersion);
                request.Write("method", notification);
                if (arguments != null)
                {
                    request.Write("params", arguments);
                }
            });
            Logger.LogDebug($"Sending notification: {requestAsJson}");
            await Send(requestAsJson, cancellationToken);
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="method"></param>
        /// <param name="payload"></param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="TransportErrorException"></exception>
        /// <returns></returns>
        public async Task<IResponse> SendRequest(string method, Json payload, CancellationToken cancellationToken = default)
        {
            var id = NextRequestId();
            var request = _json.Stringify(req =>
            {
                req.Write("jsonrpc", JsonRpcVersion);
                req.Write("id", id);
                req.Write("method", method);
                req.Write("params", payload);
            });
            
            Logger.LogDebug($"Sending request: {request}");
            await Send(request, cancellationToken);
            var response = await WaitForResponse(id, cancellationToken);
            return ParseResponse(response);        
        }

        public async Task SendOkResponse(int requestId, Json writeResult, CancellationToken cancellationToken = default)
        {
            var response = _json.Stringify(req =>
            {
                req.Write("jsonrpc", JsonRpcVersion);
                req.Write("id", requestId);
                req.Write("result", writeResult);
            });
            Logger.LogDebug($"Sending OK response: {response}");
            await Send(response, cancellationToken);
        }
        
        public async Task SendErrorResponse(int requestId, ErrorCode code, string message, Json data = null, CancellationToken cancellationToken = default)
        {
            var response = _json.Stringify(req =>
            {
                req.Write("jsonrpc", JsonRpcVersion);
                req.Write("id", requestId);
                req.Write("error", error =>
                {
                    error.Write("code", (int)code);
                    error.Write("message", message);
                    if (data != null)
                        error.Write("data", data);
                });
            });
            Logger.LogDebug($"Sending Error response: {response}");
            await Send(response, cancellationToken);
        }
        
        protected void OnMessageReceived(string messageAsJson)
        {
            try
            {
                Logger.LogDebug($"Received message: {messageAsJson}");
                var response = _json.Parse(messageAsJson);
                var idProp = response["id"];
                var method = response["method"]?.AsString();
                var methodParams = response["params"]?.AsObject();

                if (method != null)
                {
                    if (idProp == null)
                    {
                        OnNotificationReceived(method, methodParams);
                    }
                    else
                    {
                        var id = idProp.AsInt();
                        OnRequestReceived(id, method, methodParams);
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
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }

        protected virtual void OnNotificationReceived(string method, IJsonObject methodParams)
        {
            NotificationReceived?.Invoke(method, methodParams);
        }

        protected virtual void OnRequestReceived(int requestId, string method, IJsonObject methodParams)
        {
            RequestReceived?.Invoke(requestId, method, methodParams);
        }
        
        protected abstract Task OnStart(CancellationToken cancellationToken = default);
        protected abstract Task OnStop(CancellationToken cancellationToken = default);
        protected abstract Task Send(string requestAsJson, CancellationToken cancellationToken = default);

        private Task<IJsonObject> WaitForResponse(int messageId, CancellationToken cancellationToken = default)
        {
            var tsc = new TaskCompletionSource<IJsonObject>(cancellationToken);
            _tscByMessageId[messageId] = tsc;
            return tsc.Task;
        }
        
        private Response ParseResponse(IJsonObject response)
        {
            var errorProp = response["error"];
            if (errorProp == null)
                return Response.FromResult(response["result"].AsObject());
            
            var errorObj = errorProp.AsObject();
            var code = errorObj["code"].AsInt();
            var message = errorObj["message"].AsString();
            var data = errorObj["data"]?.AsObject();
            return Response.FromError(new TransportError((ErrorCode)code, message, data));
        }
        
        private int NextRequestId()
        {
            return Interlocked.Increment(ref _nextMessageId);
        }
    }
}