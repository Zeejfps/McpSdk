using System.Linq;

namespace McpSdk.Protocol.Models
{
    public sealed class CallToolResult
    {
        public static CallToolResult Ok(params Content[] content)
        {
            return new CallToolResult(content);
        }

        public static CallToolResult Ok(TextContent content)
        {
            return new CallToolResult([content]);
        }
        
        public static CallToolResult Error(params Content[] content)
        {
            return new CallToolResult(content, true);
        }
        
        public static CallToolResult Error(TextContent content)
        {
            return new CallToolResult([content], true);
        }
        
        public CallToolResult(Content[] content, bool? isError = null)
        {
            Content = content;
            IsError = isError;
        }

        public CallToolResult(IJsonObject jsonObject)
        {
            var contentArray = jsonObject["content"].AsObjectArray();
            var content = new Content[contentArray.Length];
            for (var i = 0; i < contentArray.Length; i++)
            {
                var contentObj = contentArray[i];
                content[i] = Models.Content.Create(contentObj);
            }
            Content = content;
            IsError = jsonObject["isError"]?.AsBool();
        }

        public void AsJson(IJsonWriter writer)
        {
            writer.Write("content", Content
                .Select<Content, Json>(c => c.AsJson)
                .ToArray());
            
            if (IsError.HasValue)
                writer.Write("isError", IsError.Value);
        }

        public Content[] Content { get; }
        public bool? IsError { get; }
    }
}