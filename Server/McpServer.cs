using System;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server
{
    internal sealed class McpServer : IServer
    {
        private readonly ITransport _transport;
        private readonly IToolsController _toolsController;

        public McpServer(ITransport transport, IToolsController toolsController)
        {
            _transport = transport;
            _toolsController = toolsController;
        }

        public async Task Start()
        {
            _transport.RequestReceived += OnRequestReceived;
            _transport.NotificationReceived += OnNotificationReceived;
            await _transport.Start();
        }

        private void OnRequestReceived(int requestId, string method, IJsonObject payload)
        {
            Console.Error.WriteLine($"Received request {requestId}: {method}, {payload}");
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
                
                var result = new InitializeResult(serverProtocolVersion, capabilities);
                await _transport.SendOkResponse(requestId, result.AsJson);
                
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
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
                Console.Error.WriteLine(ex);
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
                Console.Error.WriteLine(ex.Message);
                await _transport.SendErrorResponse(requestId, ErrorCode.InternalError, "Internal server error");
            }
        }

        private void OnNotificationReceived(string notification)
        {
            Console.Error.WriteLine($"Notification received: {notification}");
        }
    }
}