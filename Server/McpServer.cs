using System;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server
{
    internal sealed class McpServer : IServer
    {
        private readonly ITransport _transport;
        private readonly IToolsController _tools;

        public McpServer(ITransport transport, IToolsController tools)
        {
            _transport = transport;
            _tools = tools;
        }

        public async Task Start()
        {
            _transport.RequestReceived += OnRequestReceived;
            _transport.NotificationReceived += OnNotificationReceived;
            await _transport.Start();
        }

        private void OnRequestReceived(int requestId, string method, IJsonObject payload)
        {
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
                var initializeRequest = new InitializeRequest(reqPayload);
                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private async void OnListToolsRequestReceived(int requestId, IJsonObject reqPayload)
        {
            try
            {
                if (_tools == null)
                {
                    await _transport.SendErrorResponse(requestId, ErrorCode.MethodNotFound, "Server does not support tools");
                    return;
                }

                var result = await _tools.ListTools();
                await _transport.SendOkResponse(requestId, payload =>
                {
                    payload.Write(result.JsonObject);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                await _transport.SendErrorResponse(requestId, ErrorCode.InternalError, "Internal server error");
            }
        }

        private async void OnCallToolRequestReceived(int requestId, IJsonObject arguments)
        {
            try
            {
                if (_tools == null)
                {
                    await _transport.SendErrorResponse(requestId, ErrorCode.MethodNotFound, "Server does not support tools");
                    return;
                }
                
                var result = await _tools.CallTool(new CallToolRequest(arguments));
                await _transport.SendOkResponse(requestId, payload =>
                {
                    payload.Write(result.JsonObject);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                await _transport.SendErrorResponse(requestId, ErrorCode.InternalError, "Internal server error");
            }
        }

        private void OnNotificationReceived(string notification)
        {
            
        }
    }
}