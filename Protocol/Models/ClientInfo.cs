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

        public ClientInfo(IJsonObject jsonObject)
        {
            Name = jsonObject["Name"]?.AsString();
            Version = jsonObject["Version"]?.AsString();
        }

        public void AsJson(IJsonWriter writer)
        {
            writer.Write("name", Name);
            writer.Write("version", Version);
        }
    }
}