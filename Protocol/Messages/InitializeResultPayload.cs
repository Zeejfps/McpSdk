namespace McpSharp.Protocol.Messages
{
    public sealed class InitializeResultPayload
    {
        public string ProtocolVersion { get; }
        public ServerCapabilities Capabilities { get; }
        public ServerInfo ServerInfo { get; }

        public InitializeResultPayload(string protocolVersion, ServerCapabilities capabilities, ServerInfo serverInfo)
        {
            ProtocolVersion = protocolVersion;
            Capabilities = capabilities;
            ServerInfo = serverInfo;
        }
    }
}