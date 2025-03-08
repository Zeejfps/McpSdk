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
            var message = new InitializeRequestPayload(protocolVersion, _clientInfo)
                .WithCapability(new RootsCapability(false))
                .WithCapability(new SamplingCapability());
            var response = await _transport.SendMessage(message);
            if (response.ProtocolVersion != protocolVersion)
                throw new ClientException($"Invalid protocol version. Expected {protocolVersion}, got {response.ProtocolVersion}");
            await _transport.SendNotification(new InitializedNotification());
            IsConnected = true;
        }

        public async Task<IEnumerable<ToolInfo>> ListTools()
        {
            var request = new ListToolsRequestPayload();
            var result = await _transport.SendMessage(request);
            return result.Tools;
        }

        public Task<ICallToolResult> CallTool(string toolName)
        {
            throw new System.NotImplementedException();
        }
    }
}