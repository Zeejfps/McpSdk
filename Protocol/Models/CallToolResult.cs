using System;
using System.Linq;

namespace McpSdk.Protocol.Models
{
    public sealed class CallToolResult
    {
        public static CallToolResult Ok(params Content[] content)
        {
            return new CallToolResult(content, false);
        }

        public static CallToolResult Ok(Content content)
        {
            return new CallToolResult([content], false);
        }
        
        public static CallToolResult Error(params Content[] content)
        {
            return new CallToolResult(content, true);
        }
        
        public static CallToolResult Error(Content content)
        {
            return new CallToolResult([content], true);
        }
        
        public CallToolResult(Content[] content, bool isError)
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
            IsError = jsonObject["isError"]?.AsBool() ?? false;
        }

        public void AsJson(IJsonWriter writer)
        {
            writer.Write("content", Content
                .Select<Content, Json>(c => c.AsJson)
                .ToArray());
            writer.Write("isError", IsError);
        }

        public Content[] Content { get; }
        public bool IsError { get; }
    }
}