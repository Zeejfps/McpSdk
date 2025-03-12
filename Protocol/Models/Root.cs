namespace McpSdk.Protocol.Models
{
    public sealed class Root
    {
        public string Uri { get; }
        public string Name { get; }
        
        public Root(string uri, string name)
        {
            Uri = uri;
            Name = name;
        }

        public Root(IJsonObject jsonObject)
        {
            Uri = jsonObject["uri"]?.AsString();
            Name = jsonObject["name"]?.AsString();
        }

        public void ToJson(IJsonWriter writer)
        {
            writer.Write("uri", Uri);
            writer.Write("name", Name);
        }
    }
}