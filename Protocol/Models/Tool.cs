namespace McpSdk.Protocol.Models
{
    public sealed class Tool
    {
        public Tool(IJson json, string name, string description, IJsonObject inputSchema)
        {
            Name = name;
            Description = description;
            InputSchema = inputSchema;
            JsonObject = json.Build(props =>
            {
                props.Write("name", Name);
                props.Write("description", Description);
                props.Write("inputSchema", InputSchema);
            });
        }
        
        public Tool(IJsonObject jsonObj)
        {
            JsonObject = jsonObj;
            Name = jsonObj["name"].AsString();
            Description = jsonObj["description"].AsString();
            InputSchema = jsonObj["inputSchema"].AsObject();
        }

        public IJsonObject JsonObject { get; }
        public string Name { get; }
        public string Description { get; }
        public IJsonObject InputSchema { get; }

        public override string ToString()
        {
            return JsonObject.ToString();
        }
    }
}