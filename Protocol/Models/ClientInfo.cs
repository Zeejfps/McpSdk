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

        public void ToJson(IJsonWriter writer)
        {
            writer.Write("name", Name);
            writer.Write("version", Version);
        }
    }
}