namespace McpSdk.Protocol.Models
{
    public sealed class ModelHint : IJsonObjectWriter
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
        
        public void WriteMembers(IJsonWriter writer)
        {
            writer.Write("name", Name);
        }
    }
}