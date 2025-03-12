namespace McpSdk.Protocol.Models
{
    public sealed class InitializeResult
    {
        public string ProtocolVersion { get; }

        public InitializeResult(string protocolVersion)
        {
            ProtocolVersion = protocolVersion;
        }

        public InitializeResult(IJsonObject jsonObject)
        {
            ProtocolVersion = jsonObject["protocolVersion"]?.AsString();;
        }

        public void Write(IJsonWriter writer)
        {
            writer.Write("protocolVersion", ProtocolVersion);
        }
    }
}