namespace McpSdk.Protocol.Models
{
    public sealed class ServerInfo
    {
        public string Name { get; }
        public string Version { get; }

        public ServerInfo(string name, string version)
        {
            Name = name;
            Version = version;
        }

        public ServerInfo(IJsonObject jsonObject)
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