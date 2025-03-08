using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McpSharp.Protocol;
using McpSharp.Protocol.Messages;

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
            var protocolVersion = "2024-11-05";
            var requestPayload = new InitializeRequestPayload(protocolVersion, _clientInfo)
                .WithCapability(new RootsCapability(false))
                .WithCapability(new SamplingCapability());
            var response = await _transport.SendMessage(requestPayload);
            if (response.ProtocolVersion != protocolVersion)
                throw new ClientException($"Invalid protocol version. Expected {protocolVersion}, got {response.ProtocolVersion}");
            await _transport.SendNotification(new InitializedNotification());
            IsConnected = true;
        }

        public async Task<IEnumerable<Tool>> ListTools()
        {
            var requestPayload = new ListToolsRequestPayload();
            var result = await _transport.SendMessage(requestPayload);
            return result.Tools;
        }

        public async Task<ICallToolResult> CallTool(string toolName, Dictionary<string, object> parameters = null)
        {
            var requestPayload = new CallToolRequestPayload(toolName, parameters);
            await _transport.SendMessage(requestPayload);
            return null;
        }
    }
}