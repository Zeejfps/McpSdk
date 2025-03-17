namespace McpSdk.Protocol.Models
{
    public sealed class ResourceContent
    {
        public string Uri { get; }
        public string MimeType { get; }
        public string Text { get; }
        public string Blob { get; }
        
        public ResourceContent(IJsonObject jsonObject)
        {
            Uri = jsonObject["uri"].AsString();
            MimeType = jsonObject["mimeType"].AsString();
            Text = jsonObject["text"]?.AsString();
            Blob = jsonObject["blob"]?.AsString();
        }

        public void AsJson(IJsonWriter writer)
        {
            writer.Write("uri", Uri);
            writer.Write("mimeType", MimeType);
            
            if (Text != null)
                writer.Write("text", Text);
            
            if (Blob != null)
                writer.Write("text", Text);
        }
    }
}