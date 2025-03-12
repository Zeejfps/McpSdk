namespace McpSdk.Protocol.Models
{
    public sealed class InitializeResult
    {
        public string ProtocolVersion { get; }
        public ServerCapabilities Capabilities { get; }

        public ServerInfo ServerInfo { get; }
        
        public InitializeResult(string protocolVersion, ServerCapabilities capabilities, ServerInfo serverInfo)
        {
            ProtocolVersion = protocolVersion;
            Capabilities = capabilities;
            ServerInfo = serverInfo;
        }

        public InitializeResult(IJsonObject jsonObject)
        {
            ProtocolVersion = jsonObject["protocolVersion"]?.AsString();;
        }
        
        public void AsJson(IJsonWriter writer)
        {
            writer.Write("protocolVersion", ProtocolVersion);
            writer.Write("capabilities", Capabilities.AsJson);
            writer.Write("serverInfo", ServerInfo.AsJson);
        }
    }
}