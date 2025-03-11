namespace McpSdk.Protocol
{
    public sealed class Tool
    {
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