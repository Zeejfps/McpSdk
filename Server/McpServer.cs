using System;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Shared;

namespace McpSdk.Server
{
    internal sealed class McpServer : IServer
    {
        private readonly ITransport _transport;
        private readonly ServerInfo _serverInfo;
        private readonly IToolsController _toolsController;
        private readonly ILogger _logger;
        
        private bool _isRunning;
        
        public McpServer(ITransport transport, ServerInfo serverInfo, ILoggerFactory loggerFactory, IToolsController toolsController)
        {
            _transport = transport;
            _serverInfo = serverInfo;
            _logger = loggerFactory.Create<McpServer>();
            _toolsController = toolsController;
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

        private void OnRequestReceived(int requestId, string method, IJsonObject payload)
        {
            _logger.LogDebug($"Received Request: Id: {requestId}, Method: {method}, Payload: {payload}");
            if (method == "initialize")
            {
                OnInitializeRequestReceived(requestId, payload);
            }
            else if (method == "tools/list")
            {
                OnListToolsRequestReceived(requestId, payload);
            }
            else if (method == "tools/call")
            {
                OnCallToolRequestReceived(requestId, payload);
            }
        }

        private async void OnInitializeRequestReceived(int requestId, IJsonObject reqPayload)
        {
            try
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

                var capabilities = new ServerCapabilities();
                if (_toolsController != null)
                    capabilities.Tools = new ToolsCapability(_toolsController.IsListChangedNotificationSupported);
                
                var result = new InitializeResult(serverProtocolVersion, capabilities, _serverInfo);
                await _transport.SendOkResponse(requestId, result.AsJson);
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                await _transport.SendErrorResponse(
                    requestId,
                    ErrorCode.InternalError, "Internal server error");
            }
        }

        private async void OnListToolsRequestReceived(int requestId, IJsonObject reqPayload)
        {
            try
            {
                if (_toolsController == null)
                {
                    await _transport.SendErrorResponse(requestId, ErrorCode.MethodNotFound, "Server does not support tools");
                    return;
                }

                var result = await _toolsController.ListTools();
                await _transport.SendOkResponse(requestId, payload =>
                {
                    result.Write(payload);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                await _transport.SendErrorResponse(requestId, ErrorCode.InternalError, "Internal server error");
            }
        }

        private async void OnCallToolRequestReceived(int requestId, IJsonObject arguments)
        {
            try
            {
                if (_toolsController == null)
                {
                    await _transport.SendErrorResponse(requestId, ErrorCode.MethodNotFound, "Server does not support tools");
                    return;
                }
                
                var result = await _toolsController.CallTool(new CallToolRequest(arguments));
                await _transport.SendOkResponse(requestId, result.AsJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                await _transport.SendErrorResponse(requestId, ErrorCode.InternalError, "Internal server error");
            }
        }

        private void OnNotificationReceived(string notification)
        {
            _logger.LogDebug($"Received Notification: '{notification}'");
        }
    }
}