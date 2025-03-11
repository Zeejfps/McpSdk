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
                content[i] = Protocol.Content.Create(contentObj);
            }
            Content = content;
            IsError = jsonObject["isError"]?.AsBool() ?? false;
        }

        public IJsonObject JsonObject { get; }
        public Content[] Content { get; }
        public bool IsError { get; }

        public override string ToString()
        {
            return JsonObject.ToString();
        }
    }
}