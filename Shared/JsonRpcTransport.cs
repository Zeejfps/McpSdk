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
        private readonly JsonRpcCodec _codec;
        private readonly Dictionary<RequestId, TaskCompletionSource<IJsonObject>> _tscByMessageId = new();

        private long _nextMessageId;

        protected ILogger Logger { get; }

        protected JsonRpcTransport(IJson json, ILoggerFactory loggerFactory)
        {
            _codec = new JsonRpcCodec(json);
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
            var requestAsJson = _codec.EncodeNotification(notification, arguments);
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
            var request = _codec.EncodeRequest(id, method, payload);

            Logger.LogDebug($"Sending request: {request}");
            await Send(request, cancellationToken);
            var response = await WaitForResponse(id, cancellationToken);
            return _codec.ParseResponse(response);
        }

        public async Task SendOkResponse(RequestId requestId, Json writeResult, CancellationToken cancellationToken = default)
        {
            var response = _codec.EncodeResult(requestId, writeResult);
            Logger.LogDebug($"Sending OK response: {response}");
            await Send(response, cancellationToken);
        }

        public async Task SendErrorResponse(RequestId requestId, Error error, CancellationToken cancellationToken = default)
        {
            var response = _codec.EncodeError(requestId, error);
            Logger.LogDebug($"Sending Error response: {response}");
            await Send(response, cancellationToken);
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

                if (!_codec.TryDecode(messageAsJson, out var message))
                    return;

                switch (message.Kind)
                {
                    case JsonRpcMessageKind.Notification:
                        OnNotificationReceived(message.Method, message.Parameters);
                        break;
                    case JsonRpcMessageKind.Request:
                        OnRequestReceived(message.Id, message.Method, message.Parameters);
                        break;
                    case JsonRpcMessageKind.Response:
                        if (_tscByMessageId.TryGetValue(message.Id, out var tsc))
                        {
                            _tscByMessageId.Remove(message.Id);
                            tsc.TrySetResult(message.Raw);
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

        private Task<IJsonObject> WaitForResponse(RequestId messageId, CancellationToken cancellationToken = default)
        {
            var tsc = new TaskCompletionSource<IJsonObject>(cancellationToken);
            _tscByMessageId[messageId] = tsc;
            return tsc.Task;
        }
        
        private RequestId NextRequestId()
        {
            return new RequestId(Interlocked.Increment(ref _nextMessageId));
        }
    }
}