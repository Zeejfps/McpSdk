namespace McpSharp.Protocol.Messages
{
    public sealed class InitializeMessage
    {
        public InitializeMessage(string protocolVersion, ClientInfo clientInfo)
        {
            ProtocolVersion = protocolVersion;
            ClientInfo = clientInfo;
            Capabilities = new ClientCapabilities();
        }

        public InitializeMessage WithCapability(RootsCapability capability)
        {
            Capabilities.Roots = capability;
            return this;
        }
        
        public InitializeMessage WithCapability(SamplingCapability capability)
        {
            Capabilities.Sampling = capability;
            return this;
        }

        public string ProtocolVersion { get; }
        public ClientCapabilities Capabilities { get; }
        public ClientInfo ClientInfo { get; }
    }
}