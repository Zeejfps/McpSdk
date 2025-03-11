namespace McpSharp.Protocol
{
    public sealed class CallToolResult
    {
        public CallToolResult(IJsonObject jsonObject)
        {
            JsonObject = jsonObject;
            
            var contentArray = jsonObject["content"].AsObjectArray();
            var content = new Content[contentArray.Length];
            for (var i = 0; i < contentArray.Length; i++)
            {
                var contentObj = contentArray[i];
                var type = contentObj["type"].AsString();
                if (type == "text")
                {
                    content[i] = new TextContent(contentObj);
                }
                else if (type == "image")
                {
                    content[i] = new ImageContent(contentObj);
                }
                else if (type == "resource")
                {
                    content[i] = new ResourceContent(contentObj);
                }
                else
                {
                    content[i] = new UnknownContent(contentObj);
                }
            }
            Content = content;
        }

        public IJsonObject JsonObject { get; }
        public Content[] Content { get; }

        public override string ToString()
        {
            return JsonObject.ToString();
        }
    }
}