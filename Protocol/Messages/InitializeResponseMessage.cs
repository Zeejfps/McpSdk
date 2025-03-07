namespace McpSharp.Protocol.Messages
{
    public sealed class InitializeResponseMessage
    {
        public string ProtocolVersion { get; }
        public ServerCapabilities Capabilities { get; }
        public ServerInfo ServerInfo { get; }

        public InitializeResponseMessage(string protocolVersion, ServerCapabilities capabilities, ServerInfo serverInfo)
        {
            ProtocolVersion = protocolVersion;
            Capabilities = capabilities;
            ServerInfo = serverInfo;
        }
    }
}