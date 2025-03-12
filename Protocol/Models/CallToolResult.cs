using System;
using System.Linq;

namespace McpSdk.Protocol.Models
{
    public sealed class CallToolResult
    {
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

        public void ToJson(IJsonWriter writer)
        {
            writer.Write("content", Content
                .Select<Content, Action<IJsonWriter>>(c => c.ToJson)
                .ToArray());
            writer.Write("isError", IsError);
        }

        public Content[] Content { get; }
        public bool IsError { get; }
    }
}