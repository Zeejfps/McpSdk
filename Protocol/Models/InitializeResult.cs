namespace McpSdk.Protocol.Models
{
    public sealed class InitializeResult
    {
        public string ProtocolVersion { get; }
        public ServerCapabilities Capabilities { get; }

        public InitializeResult(string protocolVersion, ServerCapabilities capabilities)
        {
            ProtocolVersion = protocolVersion;
            Capabilities = capabilities;
        }

        public InitializeResult(IJsonObject jsonObject)
        {
            ProtocolVersion = jsonObject["protocolVersion"]?.AsString();;
        }
        
        public void AsJson(IJsonWriter writer)
        {
            writer.Write("protocolVersion", ProtocolVersion);
            writer.Write("capabilities", Capabilities.AsJson);
        }
    }
}