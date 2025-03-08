namespace McpSharp.Protocol.Messages
{
    public sealed class InitializeResponsePayload
    {
        public string ProtocolVersion { get; }
        public ServerCapabilities Capabilities { get; }
        public ServerInfo ServerInfo { get; }

        public InitializeResponsePayload(string protocolVersion, ServerCapabilities capabilities, ServerInfo serverInfo)
        {
            ProtocolVersion = protocolVersion;
            Capabilities = capabilities;
            ServerInfo = serverInfo;
        }
    }
}