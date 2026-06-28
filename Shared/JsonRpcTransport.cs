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
        private readonly IJson _json;
        private readonly Dictionary<RequestId, TaskCompletionSource<IResponse>> _tscByMessageId = new();

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
            var wire = _json.Stringify(new JsonRpcNotification(notification, arguments).WriteMembers);
            Logger.LogDebug($"Sending notification: {wire}");
            await Send(wire, cancellationToken);
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
            var wire = _json.Stringify(new JsonRpcRequest(id, method, payload).WriteMembers);

            Logger.LogDebug($"Sending request: {wire}");
            await Send(wire, cancellationToken);
            return await WaitForResponse(id, cancellationToken);
        }

        public async Task SendOkResponse(RequestId requestId, Json writeResult, CancellationToken cancellationToken = default)
        {
            var wire = _json.Stringify(JsonRpcResponse.Result(requestId, writeResult).WriteMembers);
            Logger.LogDebug($"Sending OK response: {wire}");
            await Send(wire, cancellationToken);
        }

        public async Task SendErrorResponse(RequestId requestId, Error error, CancellationToken cancellationToken = default)
        {
            var wire = _json.Stringify(JsonRpcResponse.Failure(requestId, error).WriteMembers);
            Logger.LogDebug($"Sending Error response: {wire}");
            await Send(wire, cancellationToken);
        }
        
        protected void OnMessageReceived(string messageAsJson)
        {
            try
            {
                Logger.LogDebug($"Received message: {messageAsJson}");

                // JSON-RPC batching was removed in MCP 2025-06-18; a top-level array is no longer a
                // valid frame. Reject it explicitly rather than letting the parser throw on it.
                if (JsonRpcFraming.IsBatch(messageAsJson))
                {
                    Logger.LogError("Rejected a JSON-RPC batch message; batching was removed in MCP 2025-06-18.");
                    return;
                }

                if (!JsonRpcMessage.TryParse(_json, messageAsJson, out var message))
                    return;

                switch (message)
                {
                    case JsonRpcNotification notification:
                        OnNotificationReceived(notification.Method, notification.Parameters);
                        break;
                    case JsonRpcRequest request:
                        OnRequestReceived(request.Id, request.Method, request.Parameters);
                        break;
                    case JsonRpcResponse response:
                        if (_tscByMessageId.TryGetValue(response.Id, out var tsc))
                        {
                            _tscByMessageId.Remove(response.Id);
                            tsc.TrySetResult(response.ToResponse());
                        }
                        break;
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

        private Task<IResponse> WaitForResponse(RequestId messageId, CancellationToken cancellationToken = default)
        {
            var tsc = new TaskCompletionSource<IResponse>(cancellationToken);
            _tscByMessageId[messageId] = tsc;
            return tsc.Task;
        }
        
        private RequestId NextRequestId()
        {
            return new RequestId(Interlocked.Increment(ref _nextMessageId));
        }
    }
}