namespace McpSharp.Protocol
{
    public sealed class ModelHint : JsonObjectWrapper
    {
        public ModelHint(IJsonObject jsonObject) : base(jsonObject)
        {
            Name = jsonObject["name"]?.AsString();
        }
        
        public string Name { get; }
    }
}