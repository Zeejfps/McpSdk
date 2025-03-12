namespace McpSdk.Protocol.Models
{
    public sealed class InitializeRequest
    {
        public string ProtocolVersion { get; }
        public ClientInfo ClientInfo { get; }
        public ClientCapabilities ClientCapabilities { get; }
        
        public InitializeRequest(string protocolVersion, ClientCapabilities capabilities, ClientInfo clientInfo)
        {
            ProtocolVersion = protocolVersion;
            ClientCapabilities = capabilities;
            ClientInfo = clientInfo;
        }
        
        public InitializeRequest(IJsonObject jsonObject)
        {
            ProtocolVersion = jsonObject["protocolVersion"].AsString();
            
            var capabilities = jsonObject["capabilities"].AsObject();
            ClientCapabilities = new ClientCapabilities(capabilities);
            
            var clientInfoObj = jsonObject["clientInfo"]?.AsObject();
            if (clientInfoObj != null)
                ClientInfo = new ClientInfo(clientInfoObj);
        }
        
        public void AsJson(IJsonWriter writer)
        {
            writer.Write("protocolVersion", ProtocolVersion);
            writer.Write("capabilities", ClientCapabilities.AsJson);
            writer.Write("clientInfo", ClientInfo.AsJson);
        }
    }
}