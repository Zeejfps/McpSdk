namespace McpSdk.Protocol.Models
{
    public sealed class Tool
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public ObjectSchema InputSchema { get; set; }

        public Tool() {}
        
        public Tool(string name, string description, ObjectSchema schema)
        {
            Name = name;
            Description = description;
            InputSchema = schema;
        }
        
        public Tool(IJsonObject jsonObj)
        {
            Name = jsonObj["name"].AsString();
            Description = jsonObj["description"].AsString();
            var inputSchemaObj = jsonObj["inputSchema"]?.AsObject();
            if (inputSchemaObj != null)
                InputSchema = new ObjectSchema(inputSchemaObj);
        }

        public void AsJson(IJsonWriter writer)
        {
            writer.Write("name", Name);
            writer.Write("description", Description);
            if (InputSchema != null)
                writer.Write("inputSchema", InputSchema.AsJson);
        }
    }
}