using System;
using System.Collections.Generic;
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
            _clientInfo = clientInfo;
        }

        public bool IsConnected { get; private set; }
        
        public async Task Connect()
        {
            await _transport.Connect();
            var clientProtocolVersion = "2024-11-05";
            var response = await _transport.SendMessage("initialize", payload =>
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

        public async Task<IEnumerable<Tool>> ListTools()
        {
            var result = await _transport.SendMessage("tools/list", payload => { });
            var tools = result["tools"].AsObjectArray();
            var toolsCount = tools.Length;
            var toolInfos = new Tool[toolsCount];
            for (var i = 0; i < toolsCount; i++)
            {
                var toolObj = tools[i];
                toolInfos[i] = new Tool(toolObj);
            }
            return toolInfos;
        }

        public async Task<IJsonObject> CallTool(string toolName, Action<IJsonWriter> args)
        {
            return await _transport.SendMessage("tools/call", payload =>
            {
                payload.Write("name", toolName);
                payload.Write("arguments", bodyWriter =>
                {
                    args?.Invoke(bodyWriter);
                });
            });
        }
    }
}