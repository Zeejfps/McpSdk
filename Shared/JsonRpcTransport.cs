using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Shared
{
    public abstract class JsonRpcTransport : ITransport
    {
        private const string JsonRpcVersion = "2.0";

        private readonly IJson _json;
        private readonly Dictionary<RequestId, TaskCompletionSource<IJsonObject>> _tscByMessageId = new();

        private long _nextMessageId;
        
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
                id.WriteTo(req, "id");
                req.Write("method", method);
                req.Write("params", payload);
            });
            
            Logger.LogDebug($"Sending request: {request}");
            await Send(request, cancellationToken);
            var response = await WaitForResponse(id, cancellationToken);
            return ParseResponse(response);        
        }

        public async Task SendOkResponse(RequestId requestId, Json writeResult, CancellationToken cancellationToken = default)
        {
            var response = _json.Stringify(req =>
            {
                req.Write("jsonrpc", JsonRpcVersion);
                requestId.WriteTo(req, "id");
                req.Write("result", writeResult);
            });
            Logger.LogDebug($"Sending OK response: {response}");
            await Send(response, cancellationToken);
        }

        public async Task SendErrorResponse(RequestId requestId, Error error, CancellationToken cancellationToken = default)
        {
            var response = _json.Stringify(req =>
            {
                req.Write("jsonrpc", JsonRpcVersion);
                requestId.WriteTo(req, "id");
                req.Write("error", error.AsJson);
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
                        var id = RequestId.FromJson(idProp);
                        OnRequestReceived(id, method, methodParams);
                    }
                }
                else
                {
                    if (idProp == null)
                    {
                        return;
                    }

                    var id = RequestId.FromJson(idProp);
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

        protected virtual void OnRequestReceived(RequestId requestId, string method, IJsonObject methodParams)
        {
            RequestReceived?.Invoke(requestId, method, methodParams);
        }

        protected abstract Task OnStart(CancellationToken cancellationToken = default);
        protected abstract Task OnStop(CancellationToken cancellationToken = default);
        protected abstract Task Send(string requestAsJson, CancellationToken cancellationToken = default);

        private Task<IJsonObject> WaitForResponse(RequestId messageId, CancellationToken cancellationToken = default)
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
            return Response.FromError(new Error(errorObj));
        }
        
        private RequestId NextRequestId()
        {
            return new RequestId(Interlocked.Increment(ref _nextMessageId));
        }
    }
}