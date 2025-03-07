using System.Collections.Generic;
using System.Threading.Tasks;
using McpSharp.Protocol;
using McpSharp.Protocol.Messages;

namespace McpSharp.Client
{
    internal sealed class McpClient : IClient
    {
        private readonly IConnection _connection;
        private readonly ClientInfo _clientInfo;

        public McpClient(IConnection connection, ClientInfo clientInfo)
        {
            _connection = connection;
            _clientInfo = clientInfo;
        }

        public bool IsConnected { get; private set; }
        
        public async Task Connect()
        {
            await _connection.Connect();
            var protocolVersion = "2024-11-05";
            var response = await _connection.SendMessage(new InitializeMessage(protocolVersion, _clientInfo));
            if (response.ProtocolVersion != protocolVersion)
                throw new ClientException($"Invalid protocol version. Expected {protocolVersion}, got {response.ProtocolVersion}");
            await _connection.SendNotification(new InitializedNotification());
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