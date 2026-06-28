namespace McpSdk.Protocol.Models
{
    public abstract class Content : IJsonObjectWriter
    {
        public abstract void WriteMembers(IJsonWriter writer);

        public static Content FromJsonObject(IJsonObject jsonObject)
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

            if (type == "tool_use")
            {
                return new ToolUseContent(jsonObject);
            }

            if (type == "tool_result")
            {
                return new ToolResultContent(jsonObject);
            }

            return new UnknownContent(jsonObject);
        }
    }
}