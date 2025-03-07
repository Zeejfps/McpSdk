namespace McpSharp.Protocol
{
    public readonly struct ClientInfo
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