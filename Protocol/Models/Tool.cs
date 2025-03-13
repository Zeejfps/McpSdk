namespace McpSdk.Protocol.Models
{
    public sealed class Tool
    {
        public string Name { get; }
        public string Description { get; }
        public ToolInputSchema InputSchema { get; }

        public Tool(string name, string description, ToolInputSchema schema)
        {
            Name = name;
            Description = description;
            InputSchema = schema;
        }
        
        public Tool(IJsonObject jsonObj)
        {
            Name = jsonObj["name"].AsString();
            Description = jsonObj["description"].AsString();
            InputSchema = new ToolInputSchema(jsonObj["inputSchema"].AsObject());
        }

        public void AsJson(IJsonWriter writer)
        {
            writer.Write("name", Name);
            writer.Write("description", Description);
            writer.Write("inputSchema", InputSchema.AsJson);
        }
    }
}