namespace McpSdk.Protocol.Models
{
    public sealed class Root : JsonObjectWrapper
    {
        public Root(IJson json, string uri, string name)
        {
            Uri = uri;
            Name = name;
            JsonObject = json.Object(props =>
            {
                props.Write("uri", uri);
                props.Write("name", name);
            });
        }

        public string Uri { get; }
        public string Name { get; }
        public override IJsonObject JsonObject { get; }
    }
}