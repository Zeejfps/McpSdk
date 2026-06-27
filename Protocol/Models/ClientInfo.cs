namespace McpSdk.Protocol.Models
{
    public sealed class ClientInfo : IJsonObjectWriter
    {
        public string Name { get; }
        public string Version { get; }

        /// <summary>Human-friendly display name (2025-06-18).</summary>
        public string Title { get; }

        /// <summary>Human-friendly description (2025-11-25).</summary>
        public string Description { get; }

        public ClientInfo(string name, string version, string title = null, string description = null)
        {
            Name = name;
            Version = version;
            Title = title;
            Description = description;
        }

        public ClientInfo(IJsonObject jsonObject)
        {
            Name = jsonObject["name"]?.AsString();
            Version = jsonObject["version"]?.AsString();
            Title = jsonObject["title"]?.AsString();
            Description = jsonObject["description"]?.AsString();
        }

        public void WriteMembers(IJsonWriter writer)
        {
            writer.Write("name", Name);
            writer.Write("version", Version);

            if (Title != null)
                writer.Write("title", Title);

            if (Description != null)
                writer.Write("description", Description);
        }
    }
}
