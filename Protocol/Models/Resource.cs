namespace McpSdk.Protocol.Models
{
    public sealed class Resource
    {
        public string Uri { get; }
        public string MimeType { get; }
        public string Text { get; }
        
        public Resource(IJsonObject jsonObject)
        {
            Uri = jsonObject["uri"].AsString();
            MimeType = jsonObject["mimeType"].AsString();
            Text = jsonObject["text"].AsString();
        }

        public void ToJson(IJsonWriter writer)
        {
            writer.Write("uri", Uri);
            writer.Write("mimeType", MimeType);
            writer.Write("text", Text);
        }
    }
}