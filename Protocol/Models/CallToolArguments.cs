namespace McpSdk.Protocol.Models
{
    public sealed class CallToolArguments : JsonObjectWrapper
    {
        public CallToolArguments(IJsonObject jsonObject)
        {
            JsonObject = jsonObject;
            ToolName = jsonObject["name"].AsString();
            ToolArguments = jsonObject["arguments"].AsObject();
        }
        
        public string ToolName { get; }
        public IJsonObject ToolArguments { get; }
        public override IJsonObject JsonObject { get; }
    }
}