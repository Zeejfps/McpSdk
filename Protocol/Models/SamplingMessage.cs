namespace McpSdk.Protocol.Models
{
    public sealed class SamplingMessage
    {
        public string Role { get; }
        public Content Content { get; }
        
        public SamplingMessage(IJsonObject jsonObject)
        {
            Role = jsonObject["role"].AsString();
            
            var contentObj = jsonObject["content"].AsObject();
            var contentType = contentObj["type"].AsString();
            if (contentType == "text")
            {
                Content = new TextContent(contentObj);
            }
            else if (contentType == "image")
            {
                Content = new ImageContent(contentObj);
            }
            else
            {
                Content = new UnknownContent(contentObj);
            }
        }

        public void ToJson(IJsonWriter writer)
        {
            writer.Write("role", Role);
            writer.Write("content", Content.AsJson);
        }
    }
}