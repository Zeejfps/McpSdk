namespace McpSdk.Protocol
{
    public readonly struct ServerInfo
    {
        public string Name { get; }
        public string Version { get; }

        public ServerInfo(string name, string version)
        {
            Name = name;
            Version = version;
        }
    }
}