namespace McpSdk.Protocol.Models
{
    /// <summary>
    /// A single argument a prompt template accepts (used for completion/substitution). <c>Title</c> is
    /// the human display name (2025-06-18); <c>Required</c> is emitted only when set.
    /// </summary>
    public sealed class PromptArgument : IJsonObjectWriter
    {
        public string Name { get; set; }

        /// <summary>Human-friendly display title (2025-06-18); falls back to Name when absent.</summary>
        public string Title { get; set; }

        public string Description { get; set; }

        /// <summary>Whether the caller must supply this argument; omitted when null.</summary>
        public bool? Required { get; set; }

        public PromptArgument() {}

        public PromptArgument(string name, string description = null, bool? required = null)
        {
            Name = name;
            Description = description;
            Required = required;
        }

        public PromptArgument(IJsonObject jsonObject)
        {
            Name = jsonObject["name"].AsString();
            Title = jsonObject["title"]?.AsString();
            Description = jsonObject["description"]?.AsString();
            Required = jsonObject["required"]?.AsBool();
        }

        public void WriteMembers(IJsonWriter writer)
        {
            writer.Write("name", Name);
            Title?.WriteTo(writer, "title");
            Description?.WriteTo(writer, "description");
            Required?.WriteTo(writer, "required");
        }
    }
}
