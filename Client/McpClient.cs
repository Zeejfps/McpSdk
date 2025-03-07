using System.Collections.Generic;
using System.Threading.Tasks;
using McpSharp.Protocol;
using McpSharp.Protocol.Messages;

namespace McpSharp.Client
{
    internal sealed class McpClient : IClient
    {
        private IConnection _connection;
        
        private readonly ClientInfo _clientInfo;
        private readonly IConnectionFactory _connectionFactory;

        public McpClient(IConnectionFactory connectionFactory, ClientInfo clientInfo)
        {
            _connectionFactory = connectionFactory;
            _clientInfo = clientInfo;
        }

        public bool IsConnected { get; private set; }
        
        public async Task Connect()
        {
            _connection = await _connectionFactory.CreateConnection();
            var protocolVersion = "2024-11-05";
            var response = await _connection.SendMessage(new InitializeMessage(protocolVersion, _clientInfo));
            if (response.ProtocolVersion != protocolVersion)
                throw new ClientException($"Invalid protocol version. Expected {protocolVersion}, got {response.ProtocolVersion}");
            await _connection.SendMessage(new InitializedMessage());
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