using System;
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

        public bool IsConnected { get; private set; }

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
            _transport.SendNotification("notifications/roots/list_changed");
        }

        private void OnRequestReceived(JsonRpcRequest request)
        {
            var requestId = request.Id;
            var method = request.Method;
            var args = request.Parameters;
            if (method == "roots/list")
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
            var initializeResponse = await _transport.SendRequest("initialize", initializeRequest);
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

            await _transport.SendNotification("notifications/initialized");
            
            if (_roots != null && _roots.IsListChangedNotificationSupported)
                _roots.ListChanged += OnRootsListChanged;
            
            IsConnected = true;
        }

        public async Task<ListToolsResult> ListTools(ListToolsRequest request = null)
        {
            var listRequest = request ?? new ListToolsRequest();
            var response = await _transport.SendRequest("tools/list", listRequest);
            return response.Unwrap(
                result => new ListToolsResult(result),
                error => throw new TransportErrorException(error)
            );
        }

        public async Task<CallToolResult> CallTool(CallToolRequest request)
        {
            var response = await _transport.SendRequest("tools/call", request);
            return response.Unwrap(
                result => new CallToolResult(result),
                error => throw new TransportErrorException(error)
            );
        }
    }
}