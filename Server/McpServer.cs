using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Protocol.Models.ServerCapabilities;
using McpSdk.Shared;

namespace McpSdk.Server
{
    internal delegate Task RequestHandler(int requestId, IJsonObject arguments);
    
    internal sealed class McpServer : IServer
    {
        private readonly ITransport _transport;
        private readonly ServerInfo _serverInfo;
        private readonly IToolsController _toolsController;
        private readonly IPromptController _promptController;
        private readonly IResourcesController _resourcesController;
        private readonly ILogger _logger;
        private readonly Dictionary<string, RequestHandler> _requestHandlersByPathLookup = new();

        private bool _isRunning;

        public McpServer(
            ITransport transport,
            ServerInfo serverInfo,
            ILoggerFactory loggerFactory,
            IToolsController toolsController,
            IPromptController promptController,
            IResourcesController resourcesController)
        {
            _transport = transport;
            _serverInfo = serverInfo;
            _logger = loggerFactory.Create<McpServer>();
            _toolsController = toolsController;
            _promptController = promptController;
            _resourcesController = resourcesController;

            _requestHandlersByPathLookup.Add("initialize", HandleInitializeRequest);

            if (_toolsController != null)
            {
                _requestHandlersByPathLookup.Add("tools/list", HandleListToolsRequest);
                _requestHandlersByPathLookup.Add("tools/call", HandleCallToolRequest);
            }

            if (_promptController != null)
            {
                _requestHandlersByPathLookup.Add("prompts/list", HandleListPromptsRequest);
                _requestHandlersByPathLookup.Add("prompts/get", HandleGetPromptRequest);
            }
        }

        public async Task Start()
        {
            try
            {
                if (_isRunning)
                    return;
                
                _transport.RequestReceived += OnRequestReceived;
                _transport.NotificationReceived += OnNotificationReceived;
                await _transport.Start();
                _isRunning = true;
                _logger.LogDebug("Mcp Server Started");
            }
            catch (Exception)
            {
                _transport.RequestReceived -= OnRequestReceived;
                _transport.NotificationReceived -= OnNotificationReceived;
                throw;
            }
        }

        public async Task Stop()
        {
            if (!_isRunning)
                return;

            _logger.LogDebug("Stopping Mcp Server...");
            _transport.RequestReceived -= OnRequestReceived;
            _transport.NotificationReceived -= OnNotificationReceived;
            _isRunning = false;
            
            await _transport.Stop();
            
            _logger.LogDebug("Mcp Server Stopped");
        }

        private async void OnRequestReceived(int requestId, string path, IJsonObject payload)
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
                        ErrorCode.MethodNotFound, $"Method {path} is not supported");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                await _transport.SendErrorResponse(
                    requestId,
                    ErrorCode.InternalError, "Internal server error");
            }
        }

        private async Task HandleInitializeRequest(int requestId, IJsonObject reqPayload)
        {
            var serverProtocolVersion = "2024-11-05";
            var request = new InitializeRequest(reqPayload);
            if (request.ProtocolVersion != serverProtocolVersion)
            {
                await _transport.SendErrorResponse(
                    requestId,
                    ErrorCode.InvalidParams,
                    $"Protocol mismatch. Expected {serverProtocolVersion}, received: {request.ProtocolVersion}");
                return;
            }

            var capabilities = new ServerCapabilitiesModel();
            if (_toolsController != null)
            {
                capabilities.Tools = new ToolsCapabilityModel(_toolsController.IsListChangedNotificationSupported);
            }

            if (_promptController != null)
            {
                capabilities.Prompts = new PromptsCapabilityModel(_promptController.IsListChangedNotificationSupported);
            }

            if (_resourcesController != null)
            {
                capabilities.Resources = new ResourcesCapabilityModel
                {
                    IsListChangedNotificationSupported = _resourcesController.IsListChangedNotificationSupported,
                    IsResourceChangedNotificationSupported = _resourcesController.IsListChangedNotificationSupported,
                };
            }
            
            var result = new InitializeResult(serverProtocolVersion, capabilities, _serverInfo);
            await _transport.SendOkResponse(requestId, result.AsJson);
        }

        private async Task HandleListToolsRequest(int requestId, IJsonObject reqPayload)
        {
            var result = await _toolsController.ListTools();
            await _transport.SendOkResponse(requestId, result.AsJson);
        }

        private async Task HandleCallToolRequest(int requestId, IJsonObject arguments)
        {
            var result = await _toolsController.CallTool(new CallToolRequest(arguments));
            await _transport.SendOkResponse(requestId, result.AsJson);
        }

        private async Task HandleListPromptsRequest(int requestId, IJsonObject arguments)
        {
            var result = await _promptController.ListPrompts();
            await _transport.SendOkResponse(requestId, result.AsJson);
        }

        private async Task HandleGetPromptRequest(int requestId, IJsonObject arguments)
        {
            var result = await _promptController.GetPrompt(new GetPromptRequest(arguments));
            await _transport.SendOkResponse(requestId, result.AsJson);
        }
        
        private void OnNotificationReceived(string notification, IJsonObject arguments)
        {
            _logger.LogDebug($"Received Notification: {notification}, {arguments}");
        }
    }
}