namespace McpSdk.Protocol.Models
{
    public sealed class ModelHint : IJsonSerializable
    {
        public string Name { get; }

        public ModelHint(string name)
        {
            Name = name;
        }
        
        public ModelHint(IJsonObject jsonObject)
        {
            Name = jsonObject["name"]?.AsString();
        }
        
        public void AsJson(IJsonWriter writer)
        {
            writer.Write("name", Name);
        }
    }
}