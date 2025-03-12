namespace McpSdk.Protocol.Models
{
    public sealed class ModelHint : JsonObjectWrapper
    {
        public ModelHint(IJsonObject jsonObject)
        {
            Name = jsonObject["name"]?.AsString();
            JsonObject = jsonObject;
        }
        
        public string Name { get; }
        public override IJsonObject JsonObject { get; }
    }
}