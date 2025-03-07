namespace McpSharp.Protocol.Messages
{
    public sealed class InitializeMessage
    {
        public InitializeMessage(string protocolVersion, ClientInfo clientInfo)
        {
            ProtocolVersion = protocolVersion;
            ClientInfo = clientInfo;
        }

        public string ProtocolVersion { get; }
        public ClientInfo ClientInfo { get; }
    }
}