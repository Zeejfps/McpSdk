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
        private readonly ILogger _logger;
        
        public bool IsConnected { get; private set; }
        
        public McpClient(ITransport transport, ILoggerFactory loggerFactory, ClientInfo clientInfo, IRootsController roots, ISamplingController sampling)
        {
            _transport = transport;
            _logger = loggerFactory.Create<McpClient>();
            _roots = roots;
            _clientInfo = clientInfo;
            _sampling = sampling;
        }

        private void OnRootsListChanged()
        {
            _transport.SendNotification("notifications/roots/list_changed");
        }

        private void OnRequestReceived(int requestId, string method, IJsonObject args)
        {
            if (method == "roots/list")
            {
                OnListRootsRequestReceived(requestId, args);
            }
            else if (method == "sampling/createMessage")
            {
                OnCreateMessageRequestReceived(requestId, args);
            }
        }

        private async void OnCreateMessageRequestReceived(int requestId, IJsonObject methodParams)
        {
            try
            {
                var sampling = _sampling;
                if (sampling == null)
                    return;
                
                var request = new CreateMessageRequest(methodParams);
                var result = await sampling.CreateMessages(request);
                await _transport.SendOkResponse(requestId, result.AsJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                await _transport.SendErrorResponse(
                    requestId,
                    new Error(ErrorCode.InternalError, "Internal client error")
                );
            }
        }

        private async void OnListRootsRequestReceived(int requestId, IJsonObject _)
        {
            try
            {
                if (_roots == null)
                    return;

                var result = await _roots.ListRoots().ConfigureAwait(false);
                await _transport.SendOkResponse(requestId, result.AsJson).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                await _transport.SendErrorResponse(
                    requestId, 
                    new Error(ErrorCode.InternalError, "Internal client error")
                );
            }
        }

        private void OnNotificationReceived(string notification, IJsonObject args)
        {
            _logger.LogDebug($"Notification Received: {notification}, {args}");
        }
        
        public async Task Connect()
        {
            if (IsConnected)
                throw new Exception("Client is already connected");
            
            _transport.RequestReceived += OnRequestReceived;
            _transport.NotificationReceived += OnNotificationReceived;
            await _transport.Start();
            
            var clientProtocolVersion = "2024-11-05";
            var capabilities = new ClientCapabilitiesModel();
            if (_roots != null)
                capabilities.RootsCapability = new RootsCapabilityModel(_roots.IsListChangedNotificationSupported);

            if (_sampling != null)
                capabilities.SamplingCapability = new SamplingCapabilityModel();
            
            var initializeRequest = new InitializeRequest(clientProtocolVersion, capabilities, _clientInfo);
            var initializeResponse = await _transport.SendRequest("initialize", initializeRequest.AsJson);
            var initializeResult = initializeResponse.Unwrap(
                result => new InitializeResult(result),
                error => throw new TransportErrorException(error));
            
            var serverProtocolVersion = initializeResult.ProtocolVersion;
            if (serverProtocolVersion != clientProtocolVersion)
                throw new ClientException($"Invalid protocol version. Expected {clientProtocolVersion}, got {serverProtocolVersion}");
            
            await _transport.SendNotification("initialized");
            
            if (_roots != null && _roots.IsListChangedNotificationSupported)
                _roots.ListChanged += OnRootsListChanged;
            
            IsConnected = true;
        }

        public async Task<ListToolsResult> ListTools()
        {
            var response = await _transport.SendRequest("tools/list", payload => { });
            return response.Unwrap(
                result => new ListToolsResult(result), 
                error => throw new TransportErrorException(error)
            );
        }

        public async Task<CallToolResult> CallTool(CallToolRequest request)
        {
            var response = await _transport.SendRequest("tools/call", request.AsJson);
            return response.Unwrap(
                result => new CallToolResult(result),
                error => throw new TransportErrorException(error)
            );
        }
    }
}