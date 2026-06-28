using System;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Protocol.Models.ClientCapabilities;
using McpSdk.Shared;

namespace McpSdk.Client
{
    internal sealed class McpClient : IClient
    {
        private readonly ITransport _transport;
        private readonly ClientInfo _clientInfo;
        private readonly IRootsController _roots;
        private readonly ISamplingController _sampling;
        private readonly IElicitationController _elicitation;
        private readonly ILogger _logger;

        private long _nextRequestId;

        public bool IsConnected { get; private set; }

        public event Action<LogMessage> LogMessageReceived;
        public event Action<ProgressNotification> ProgressReceived;

        public McpClient(ITransport transport, ILoggerFactory loggerFactory, ClientInfo clientInfo, IRootsController roots, ISamplingController sampling, IElicitationController elicitation)
        {
            _transport = transport;
            _logger = loggerFactory.Create<McpClient>();
            _roots = roots;
            _clientInfo = clientInfo;
            _sampling = sampling;
            _elicitation = elicitation;
        }

        private void OnRootsListChanged()
        {
            SendNotification("notifications/roots/list_changed");
        }

        private RequestId NextRequestId() => new RequestId(Interlocked.Increment(ref _nextRequestId));

        /// <summary>
        /// Stamps a fresh sender-owned id, wraps the call as a <see cref="JsonRpcRequest"/>, and hands it to
        /// the transport for sending + response correlation.
        /// </summary>
        private async Task<JsonRpcResponse> SendRequest(string method, IJsonObjectWriter parameters, CancellationToken cancellationToken = default)
        {
            var id = NextRequestId();
            var request = new JsonRpcRequest(id, method, parameters.WriteMembers);
            // When the caller cancels, tell the server so it can stop processing (the canceller's duty).
            using (cancellationToken.Register(() => SendCancelled(id)))
                return await _transport.SendRequest(request, cancellationToken).ConfigureAwait(false);
        }

        private void SendCancelled(RequestId id)
        {
            try
            {
                _ = SendNotification("notifications/cancelled", w => id.WriteTo(w, "requestId"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
            }
        }

        private Task SendNotification(string method, Json arguments = null, CancellationToken cancellationToken = default)
            => _transport.SendNotification(new JsonRpcNotification(method, arguments), cancellationToken);

        private void OnRequestReceived(JsonRpcRequest request)
        {
            var requestId = request.Id;
            var method = request.Method;
            var args = request.Parameters;
            if (method == "ping")
            {
                // Base-protocol utility: reply promptly with an empty result.
                _ = _transport.SendResponse(JsonRpcResponse.Ok(requestId, _ => { }));
            }
            else if (method == "roots/list")
            {
                OnListRootsRequestReceived(requestId, args);
            }
            else if (method == "sampling/createMessage")
            {
                OnCreateMessageRequestReceived(requestId, args);
            }
            else if (method == "elicitation/create")
            {
                OnElicitationRequestReceived(requestId, args);
            }
            else
            {
                // Every other server->client request must get a JSON-RPC error, not be dropped:
                // a silently-ignored request would leave the server awaiting a reply forever.
                _ = _transport.SendResponse(JsonRpcResponse.Failure(
                    requestId,
                    new Error(ErrorCode.MethodNotFound, $"Method '{method}' is not supported by this client")
                ));
            }
        }

        private async void OnElicitationRequestReceived(RequestId requestId, IJsonObject methodParams)
        {
            try
            {
                var elicitation = _elicitation;
                if (elicitation == null)
                {
                    await _transport.SendResponse(JsonRpcResponse.Failure(
                        requestId,
                        new Error(ErrorCode.MethodNotFound, "Elicitation is not supported by this client")
                    ));
                    return;
                }

                var request = new ElicitRequest(methodParams);

                // Reject a mode the client never advertised (spec: -32602 for an undeclared mode).
                var unsupportedMode =
                    (request.IsUrlMode && !elicitation.SupportsUrlMode) ||
                    (!request.IsUrlMode && !elicitation.SupportsFormMode);
                if (unsupportedMode)
                {
                    await _transport.SendResponse(JsonRpcResponse.Failure(
                        requestId,
                        new Error(ErrorCode.InvalidParams, $"Elicitation mode '{request.Mode ?? ElicitRequest.ModeForm}' is not supported")
                    ));
                    return;
                }

                var result = await elicitation.Elicit(request);
                await _transport.SendResponse(JsonRpcResponse.Ok(requestId, result.WriteMembers));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                await _transport.SendResponse(JsonRpcResponse.Failure(
                    requestId,
                    new Error(ErrorCode.InternalError, "Internal client error")
                ));
            }
        }

        private async void OnCreateMessageRequestReceived(RequestId requestId, IJsonObject methodParams)
        {
            try
            {
                var sampling = _sampling;
                if (sampling == null)
                    return;
                
                var request = new CreateMessageRequest(methodParams);
                var result = await sampling.CreateMessages(request);
                await _transport.SendResponse(JsonRpcResponse.Ok(requestId, result.WriteMembers));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                await _transport.SendResponse(JsonRpcResponse.Failure(
                    requestId,
                    new Error(ErrorCode.InternalError, "Internal client error")
                ));
            }
        }

        private async void OnListRootsRequestReceived(RequestId requestId, IJsonObject _)
        {
            try
            {
                if (_roots == null)
                    return;

                var result = await _roots.ListRoots().ConfigureAwait(false);
                await _transport.SendResponse(JsonRpcResponse.Ok(requestId, result.WriteMembers)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                await _transport.SendResponse(JsonRpcResponse.Failure(
                    requestId,
                    new Error(ErrorCode.InternalError, "Internal client error")
                ));
            }
        }

        private void OnNotificationReceived(JsonRpcNotification notification)
        {
            _logger.LogDebug($"Notification Received: {notification.Method}, {notification.Parameters}");

            // Route server->client notifications to their handlers instead of dropping them.
            if (notification.Parameters == null)
                return;

            switch (notification.Method)
            {
                case "notifications/message":
                    LogMessageReceived?.Invoke(new LogMessage(notification.Parameters));
                    break;
                case "notifications/progress":
                    ProgressReceived?.Invoke(new ProgressNotification(notification.Parameters));
                    break;
            }
        }
        
        public async Task Connect()
        {
            if (IsConnected)
                throw new Exception("Client is already connected");
            
            _transport.RequestReceived += OnRequestReceived;
            _transport.NotificationReceived += OnNotificationReceived;
            await _transport.Start();
            
            var clientProtocolVersion = ProtocolVersion.Latest;
            var capabilities = new ClientCapabilitiesModel();
            if (_roots != null)
                capabilities.RootsCapability = new RootsCapabilityModel(_roots.IsListChangedNotificationSupported);

            if (_sampling != null)
                capabilities.SamplingCapability = new SamplingCapabilityModel(_sampling.SupportsTools);

            if (_elicitation != null)
                capabilities.ElicitationCapability =
                    new ElicitationCapabilityModel(_elicitation.SupportsFormMode, _elicitation.SupportsUrlMode);

            var initializeRequest = new InitializeRequest(clientProtocolVersion, capabilities, _clientInfo);
            var initializeResponse = await SendRequest("initialize", initializeRequest);
            var initializeResult = initializeResponse.Unwrap(
                result => new InitializeResult(result),
                error => throw new TransportErrorException(error));
            
            var serverProtocolVersion = initializeResult.ProtocolVersion;
            if (!ProtocolVersion.IsSupported(serverProtocolVersion))
            {
                await _transport.Stop();
                throw new ClientException(
                    $"Server responded with unsupported protocol version '{serverProtocolVersion}'. " +
                    $"Supported versions: {string.Join(", ", ProtocolVersion.Supported)}");
            }

            await SendNotification("notifications/initialized");
            
            if (_roots != null && _roots.IsListChangedNotificationSupported)
                _roots.ListChanged += OnRootsListChanged;
            
            IsConnected = true;
        }

        public async Task Ping(CancellationToken cancellationToken = default)
        {
            var response = await _transport
                .SendRequest(new JsonRpcRequest(NextRequestId(), "ping", (Json)(_ => { })), cancellationToken)
                .ConfigureAwait(false);
            if (response.IsError)
                throw new TransportErrorException(response.Error);
        }

        public async Task SetLoggingLevel(LoggingLevel level, CancellationToken cancellationToken = default)
        {
            var response = await SendRequest("logging/setLevel", new SetLevelRequest(level), cancellationToken)
                .ConfigureAwait(false);
            if (response.IsError)
                throw new TransportErrorException(response.Error);
        }

        public async Task<ListToolsResult> ListTools(ListToolsRequest request = null, CancellationToken cancellationToken = default)
        {
            var listRequest = request ?? new ListToolsRequest();
            var response = await SendRequest("tools/list", listRequest, cancellationToken);
            return response.Unwrap(
                result => new ListToolsResult(result),
                error => throw new TransportErrorException(error)
            );
        }

        public async Task<CallToolResult> CallTool(CallToolRequest request, CancellationToken cancellationToken = default)
        {
            var response = await SendRequest("tools/call", request, cancellationToken);
            return response.Unwrap(
                result => new CallToolResult(result),
                error => throw new TransportErrorException(error)
            );
        }
    }
}