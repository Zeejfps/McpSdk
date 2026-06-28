using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;

namespace McpSdk.Shared
{
    /// <summary>
    /// The shared JSON-RPC engine and the one base every transport extends. It owns everything that is the
    /// same across transports — request/response correlation, inbound dispatch into
    /// <see cref="RequestReceived"/> / <see cref="NotificationReceived"/>, and the
    /// <see cref="ITransport"/> send surface — and works purely in <see cref="JsonRpcMessage"/> models, so
    /// the engine never touches JSON itself.
    ///
    /// A concrete transport supplies only the wire: it implements <see cref="SendMessage"/> to render and
    /// route an outbound message, runs its own read loop in <see cref="OnStart"/>, and calls
    /// <see cref="OnMessageReceived"/> for each inbound message it parses off the wire. Everything above
    /// that — correlation, the MCP protocol — is inherited. <see cref="McpServer"/> / <see cref="McpClient"/>
    /// depend only on <see cref="ITransport"/> and are unaware which transport sits underneath.
    ///
    /// Per JSON-RPC 2.0 the request id is the sender's to choose, so id generation lives with the sender
    /// (e.g. <see cref="McpClient"/>); this base only matches a reply back to its awaiting request.
    /// </summary>
    public abstract class JsonRpcTransport : ITransport
    {
        private readonly object _lock = new object();
        private readonly Dictionary<RequestId, TaskCompletionSource<JsonRpcResponse>> _pending = new();

        protected JsonRpcTransport(ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.Create(GetType());
        }

        protected ILogger Logger { get; }

        public event RequestReceivedCallback RequestReceived;
        public event NotificationReceivedCallback NotificationReceived;

        public Task Start(CancellationToken cancellationToken = default) => OnStart(cancellationToken);

        public async Task Stop()
        {
            // The connection is closing; fail any in-flight requests rather than letting them hang.
            List<TaskCompletionSource<JsonRpcResponse>> pending;
            lock (_lock)
            {
                pending = new List<TaskCompletionSource<JsonRpcResponse>>(_pending.Values);
                _pending.Clear();
            }
            foreach (var tcs in pending)
                tcs.TrySetCanceled();

            await OnStop().ConfigureAwait(false);
        }

        public async Task<JsonRpcResponse> SendRequest(JsonRpcRequest request, CancellationToken cancellationToken = default)
        {
            // Register the pending response BEFORE sending, so a transport that delivers the reply
            // synchronously (e.g. an in-memory loopback) can never complete it before we are listening.
            var tcs = new TaskCompletionSource<JsonRpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_lock)
                _pending[request.Id] = tcs;

            await SendMessage(request, cancellationToken).ConfigureAwait(false);

            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
                return await tcs.Task.ConfigureAwait(false);
        }

        public Task SendNotification(JsonRpcNotification notification, CancellationToken cancellationToken = default)
            => SendMessage(notification, cancellationToken);

        public Task SendResponse(JsonRpcResponse response, CancellationToken cancellationToken = default)
            => SendMessage(response, cancellationToken);

        /// <summary>Renders one outbound message and puts it on the wire (a transport may inspect it to
        /// route — e.g. an HTTP transport returns a response on the POST that carried its request).</summary>
        protected abstract Task SendMessage(JsonRpcMessage message, CancellationToken cancellationToken = default);

        /// <summary>Starts the transport's wire and read loop. Inbound messages are surfaced via
        /// <see cref="OnMessageReceived"/>.</summary>
        protected abstract Task OnStart(CancellationToken cancellationToken = default);

        /// <summary>Stops the transport's wire and releases its resources.</summary>
        protected abstract Task OnStop();

        /// <summary>Called by a concrete transport for each message parsed off the wire: dispatches
        /// requests/notifications to subscribers and correlates a response to its awaiting request.</summary>
        protected void OnMessageReceived(JsonRpcMessage message)
        {
            try
            {
                switch (message)
                {
                    case JsonRpcNotification notification:
                        NotificationReceived?.Invoke(notification);
                        break;
                    case JsonRpcRequest request:
                        RequestReceived?.Invoke(request);
                        break;
                    case JsonRpcResponse response:
                        TaskCompletionSource<JsonRpcResponse> tcs;
                        lock (_lock)
                        {
                            if (_pending.TryGetValue(response.Id, out tcs))
                                _pending.Remove(response.Id);
                        }
                        tcs?.TrySetResult(response);
                        break;
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }
    }
}
