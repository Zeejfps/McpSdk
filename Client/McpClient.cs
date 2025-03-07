using System.Collections.Generic;
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
            var response = await _transport.SendMessage(new InitializeMessage(protocolVersion, _clientInfo));
            if (response.ProtocolVersion != protocolVersion)
                throw new ClientException($"Invalid protocol version. Expected {protocolVersion}, got {response.ProtocolVersion}");
            await _transport.SendNotification(new InitializedNotification());
            IsConnected = true;
        }

        public Task<IEnumerable<IToolInfo>> ListTools()
        {
            throw new System.NotImplementedException();
        }

        public Task<ICallToolResult> CallTool(string toolName)
        {
            throw new System.NotImplementedException();
        }
    }
}