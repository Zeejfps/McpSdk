namespace McpSdk.Protocol.Models
{
    public abstract class Content
    {
        public abstract void AsJson(IJsonWriter writer);
        
        public static Content Create(IJsonObject jsonObject)
        {
            var type = jsonObject["type"].AsString();
            if (type == "text")
            {
                return new TextContent(jsonObject);
            }
            
            if (type == "image")
            {
                return new ImageContent(jsonObject);
            }

            if (type == "audio")
            {
                return new AudioContent(jsonObject);
            }

            if (type == "resource")
            {
                return new EmbeddedResourceContent(jsonObject);
            }

            if (type == "resource_link")
            {
                return new ResourceLinkContent(jsonObject);
            }

            return new UnknownContent(jsonObject);
        }
    }
}