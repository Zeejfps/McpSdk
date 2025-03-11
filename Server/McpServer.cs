using System;
using System.Linq;
using System.Threading.Tasks;
using McpSdk.Protocol;

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

        private void OnRequestReceived(int requestId, string method, IJsonObject methodParams)
        {
            if (method == "tools/list")
            {
                OnListToolsRequestReceived(requestId, methodParams);
            }
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
                    var tools = result
                        .Tools
                        .Select(tool => tool.JsonObject)
                        .ToArray();
                    payload.Write("tools", tools);
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