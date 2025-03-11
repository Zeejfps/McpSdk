using System;
using System.Threading.Tasks;
using McpSharp.Protocol;

namespace McpSharp.Client
{
    internal sealed class McpClient : IClient
    {
        private readonly ITransport _transport;
        private readonly ClientInfo _clientInfo;

        public McpClient(ITransport transport, ClientInfo clientInfo)
        {
            _transport = transport;
            _transport.RequestReceived += OnRequestReceived;
            _transport.NotificationReceived += OnNotificationReceived;
            _clientInfo = clientInfo;
        }

        private void OnRequestReceived(string method, IJsonObject args)
        {
            
        }

        private void OnNotificationReceived(string notification)
        {
            
        }

        public bool IsConnected { get; private set; }
        
        public async Task Connect()
        {
            await _transport.Connect();
            var clientProtocolVersion = "2024-11-05";
            var response = await _transport.SendRequest("initialize", payload =>
            {
                payload.Write("protocolVersion", "2024-11-05");
                payload.Write("capabilities", capabilities =>
                {
                    payload.Write("roots", roots => { });
                    payload.Write("sampling", sampling => { });
                });
                payload.Write("clientInfo", clientInfo =>
                {
                    payload.Write("name", _clientInfo.Name);
                    payload.Write("version", _clientInfo.Version);
                });
            });
            
            var serverProtocolVersion = response["protocolVersion"].AsString();
            if (serverProtocolVersion != clientProtocolVersion)
                throw new ClientException($"Invalid protocol version. Expected {clientProtocolVersion}, got {serverProtocolVersion}");
            
            await _transport.SendNotification("initialized");
            IsConnected = true;
        }

        public async Task<ListToolsResult> ListTools()
        {
            var result = await _transport.SendRequest("tools/list", payload => { });
            return new ListToolsResult(result);
        }

        public async Task<CallToolResult> CallTool(string toolName, Action<IJsonWriter> args)
        {
            var result = await _transport.SendRequest("tools/call", payload =>
            {
                payload.Write("name", toolName);
                payload.Write("arguments", bodyWriter =>
                {
                    args?.Invoke(bodyWriter);
                });
            });
            return new CallToolResult(result);
        }
    }
}