namespace McpSharp.Protocol.Messages
{
    public sealed class InitializeResponseMessage
    {
        public string ProtocolVersion { get; }
        public ServerCapabilities Capabilities { get; }
        public ServerInfo ServerInfo { get; }
    }
}