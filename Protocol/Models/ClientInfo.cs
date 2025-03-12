namespace McpSdk.Protocol.Models
{
    public sealed class ClientInfo
    {
        public string Name { get; }
        public string Version { get; }

        public ClientInfo(string name, string version)
        {
            Name = name;
            Version = version;
        }
    }
}