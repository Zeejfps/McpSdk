namespace McpSharp.Protocol.Messages
{
    public sealed class InitializeRequestPayload
    {
        public InitializeRequestPayload(string protocolVersion, ClientInfo clientInfo)
        {
            ProtocolVersion = protocolVersion;
            ClientInfo = clientInfo;
            Capabilities = new ClientCapabilities();
        }

        public InitializeRequestPayload WithCapability(RootsCapability capability)
        {
            Capabilities.Roots = capability;
            return this;
        }
        
        public InitializeRequestPayload WithCapability(SamplingCapability capability)
        {
            Capabilities.Sampling = capability;
            return this;
        }

        public string ProtocolVersion { get; }
        public ClientCapabilities Capabilities { get; }
        public ClientInfo ClientInfo { get; }
    }
}