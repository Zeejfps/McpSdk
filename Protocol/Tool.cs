namespace McpSharp.Protocol
{
    public sealed class Tool
    {
        public Tool(string toolName, string description, IJsonObject inputSchema)
        {
            Name = toolName;
            Description = description;
            InputSchema = inputSchema;
        }

        public string Name { get; }
        public string Description { get; }
        public IJsonObject InputSchema { get; }

        public override string ToString()
        {
            return $"{nameof(Name)}: {Name}, {nameof(Description)}: {Description}, {nameof(InputSchema)}: {InputSchema}";
        }
    }
}