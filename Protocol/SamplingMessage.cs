namespace McpSdk.Protocol
{
    public sealed class SamplingMessage
    {
        public SamplingMessage(IJsonObject jsonObject)
        {
            JsonObject = jsonObject;
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

        public IJsonObject JsonObject { get; }
        
        public string Role { get; }
        public Content Content { get; }
    }
}