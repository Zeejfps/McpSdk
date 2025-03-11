namespace McpSdk.Protocol
{
    public sealed class CallToolArguments : JsonObjectWrapper
    {
        public CallToolArguments(IJsonObject jsonObject)
        {
            JsonObject = jsonObject;
            ToolName = jsonObject["name"].AsString();
            Arguments = jsonObject["arguments"].AsObject();
        }
        
        public string ToolName { get; }
        public IJsonObject Arguments { get; }
        public override IJsonObject JsonObject { get; }
    }
}