using System;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server
{
    internal sealed class McpServer : IServer
    {
        private readonly ITransport _transport;
        private readonly IToolsCapability _tools;

        public McpServer(ITransport transport, IToolsCapability tools)
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

        private void OnRequestReceived(int requestId, string method, IJsonObject arguments)
        {
            if (method == "initialize")
            {
                OnInitializeRequestReceived(requestId, arguments);
            }
            else if (method == "tools/list")
            {
                OnListToolsRequestReceived(requestId, arguments);
            }
            else if (method == "tools/call")
            {
                OnCallToolRequestReceived(requestId, arguments);
            }
        }

        private void OnInitializeRequestReceived(int requestId, IJsonObject arguments)
        {
            throw new NotImplementedException();
        }

        private async void OnListToolsRequestReceived(int requestId, IJsonObject methodParams)
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
                
                var result = await _tools.CallTool(new CallToolArguments(arguments));
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