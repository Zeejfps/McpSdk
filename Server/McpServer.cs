using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Protocol.Models.ServerCapabilities;
using McpSdk.Shared;

namespace McpSdk.Server
{
    internal delegate Task RequestHandler(RequestId requestId, IJsonObject arguments);
    
    internal sealed class McpServer : IServer
    {
        private readonly ITransport _transport;
        private readonly ServerInfo _serverInfo;
        private readonly IToolsController _toolsController;
        private readonly IPromptController _promptController;
        private readonly IResourcesController _resourcesController;
        private readonly ILogger _logger;
        private readonly Dictionary<string, RequestHandler> _requestHandlersByPathLookup = new();
        private readonly ServerCapabilitiesModel _capabilities = new();
        
        private bool _isRunning;

        public McpServer(
            ITransport transport,
            ServerInfo serverInfo,
            ILoggerFactory loggerFactory,
            IToolsController toolsController,
            IPromptController promptController,
            IResourcesController resourcesController)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _serverInfo = serverInfo ?? throw new ArgumentNullException(nameof(serverInfo));
            _logger = loggerFactory.Create<McpServer>();
            
            _toolsController = toolsController;
            _promptController = promptController;
            _resourcesController = resourcesController;

            _requestHandlersByPathLookup.Add("initialize", HandleInitializeRequest);

            // NOTE(Zee): Is there a better way to do this?
            if (_promptController != null)
            {
                _capabilities.Prompts = new PromptsCapabilityModel(_promptController.IsListChangedNotificationSupported);
                _requestHandlersByPathLookup.Add("prompts/list", HandleListPromptsRequest);
                _requestHandlersByPathLookup.Add("prompts/get", HandleGetPromptRequest);
                if (_promptController.IsListChangedNotificationSupported)
                    _promptController.ListChanged += OnPromptsListChanged;
            }
            
            if (_resourcesController != null)
            {
                _capabilities.Resources = new ResourcesCapabilityModel
                {
                    IsListChangedNotificationSupported = _resourcesController.IsListChangedNotificationSupported,
                    IsResourceChangedNotificationSupported = _resourcesController.IsListChangedNotificationSupported,
                };
                _requestHandlersByPathLookup.Add("resources/list", HandleListResourcesRequest);
                _requestHandlersByPathLookup.Add("resources/read", HandleReadResourceRequest);
                _requestHandlersByPathLookup.Add("resources/templates/list", HandleListResourceTemplatesRequest);
            }
            
            if (_toolsController != null)
            {
                _capabilities.Tools = new ToolsCapabilityModel(_toolsController.IsListChangedNotificationSupported);
                _requestHandlersByPathLookup.Add("tools/list", HandleListToolsRequest);
                _requestHandlersByPathLookup.Add("tools/call", HandleCallToolRequest);
            }
        }

        private async void OnPromptsListChanged()
        {
            SendNotification("notifications/prompts/list_changed");
        }

        public async Task Start()
        {
            try
            {
                if (_isRunning)
                    return;
              
                RegisterListeners();
                await _transport.Start();
                _isRunning = true;
                _logger.LogDebug("Mcp Server Started");
            }
            catch (Exception)
            {
                UnregisterListeners();
                throw;
            }
        }

        public async Task Stop()
        {
            if (!_isRunning)
                return;

            UnregisterListeners();
            _logger.LogDebug("Stopping Mcp Server...");
            _isRunning = false;
            await _transport.Stop();
            _logger.LogDebug("Mcp Server Stopped");
        }

        private void RegisterListeners()
        {
            if (_toolsController != null)
                _toolsController.ListChanged += ToolsControllerOnListChanged;
                
            _transport.RequestReceived += OnRequestReceived;
            _transport.NotificationReceived += OnNotificationReceived;
        }

        private void UnregisterListeners()
        {
            if (_toolsController != null)
                _toolsController.ListChanged -= ToolsControllerOnListChanged;
            
            _transport.RequestReceived -= OnRequestReceived;
            _transport.NotificationReceived -= OnNotificationReceived;
        }

        private async void ToolsControllerOnListChanged()
        {
            try
            {
                await _transport.SendNotification("notifications/tools/list_changed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
            }
        }

        private async void OnRequestReceived(RequestId requestId, string path, IJsonObject payload)
        {
            try
            {
                _logger.LogDebug($"Received Request: Id: {requestId}, Method: {path}, Payload: {payload}");
                if (_requestHandlersByPathLookup.TryGetValue(path, out var requestHandler))
                {
                    await requestHandler.Invoke(requestId, payload);
                }
                else
                {
                    await _transport.SendErrorResponse(
                        requestId,
                        new Error(
                            code: ErrorCode.MethodNotFound,
                            message: $"Method '{path}' is not supported")
                        );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                await _transport.SendErrorResponse(
                    requestId,
                    new Error(code: ErrorCode.InternalError, message: "Internal server error")
                );
            }
        }

        private async Task HandleInitializeRequest(RequestId requestId, IJsonObject reqPayload)
        {
            var request = new InitializeRequest(reqPayload);

            // Negotiate: honour the client's requested version when we support it,
            // otherwise offer our latest. We never error on a version mismatch.
            var negotiatedVersion = ProtocolVersion.IsSupported(request.ProtocolVersion)
                ? request.ProtocolVersion
                : ProtocolVersion.Latest;

            var result = new InitializeResult(negotiatedVersion, _capabilities, _serverInfo);
            await _transport.SendOkResponse(requestId, result.AsJson);
        }

        private async Task HandleListToolsRequest(RequestId requestId, IJsonObject reqPayload)
        {
            var result = await _toolsController.ListTools();
            await _transport.SendOkResponse(requestId, result.AsJson);
        }

        private async Task HandleCallToolRequest(RequestId requestId, IJsonObject arguments)
        {
            var result = await _toolsController.CallTool(new CallToolRequest(arguments));
            await _transport.SendOkResponse(requestId, result.AsJson);
        }

        private async Task HandleListPromptsRequest(RequestId requestId, IJsonObject arguments)
        {
            var result = await _promptController.ListPrompts();
            await _transport.SendOkResponse(requestId, result.AsJson);
        }

        private async Task HandleGetPromptRequest(RequestId requestId, IJsonObject arguments)
        {
            var result = await _promptController.GetPrompt(new GetPromptRequest(arguments));
            await _transport.SendOkResponse(requestId, result.AsJson);
        }
        
        private async Task HandleListResourceTemplatesRequest(RequestId requestId, IJsonObject arguments)
        {
            var result = await _resourcesController.ListTemplates(new ListTemplatesRequest(arguments));
            await _transport.SendOkResponse(requestId, result.AsJson);
        }

        private async Task HandleReadResourceRequest(RequestId requestId, IJsonObject arguments)
        {
            var result = await _resourcesController.ReadResource(new ReadResourceRequest(arguments));
            await _transport.SendOkResponse(requestId, result.AsJson);
        }

        private async Task HandleListResourcesRequest(RequestId requestId, IJsonObject arguments)
        {
            var result = await _resourcesController.ListResources(new ListResourcesRequest(arguments));
            await _transport.SendOkResponse(requestId, result.AsJson);
        }
        
        private void OnNotificationReceived(string notification, IJsonObject arguments)
        {
            _logger.LogDebug($"Received Notification: {notification}, {arguments}");
        }

        private async void SendNotification(string notification, Json arguments = null)
        {
            try
            {
                await _transport.SendNotification(notification, arguments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
            }
        }
    }
}