namespace McpSharp.Protocol
{
    public sealed class ToolInfo
    {
        public ToolInfo(string toolName, string description)
        {
            Name = toolName;
            Description = description;
        }

        public string Name { get; }
        public string Description { get; }

        public override string ToString()
        {
            return $"{nameof(Name)}: {Name}, {nameof(Description)}: {Description}";
        }
    }
}