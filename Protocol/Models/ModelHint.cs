namespace McpSdk.Protocol.Models
{
    public sealed class ModelHint
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
        
        public void ToJson(IJsonWriter writer)
        {
            writer.Write("name", Name);
        }
    }
}