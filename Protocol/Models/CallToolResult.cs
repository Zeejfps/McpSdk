using System.Linq;

namespace McpSdk.Protocol.Models
{
    public sealed class CallToolResult : JsonObjectWrapper
    {
        public CallToolResult(IJson json, Content[] content, bool isError)
        {
            Content = content;
            IsError = isError;
            JsonObject = json.Object(props =>
            {
                props.Write("content", content.Select(c => c.JsonObject).ToArray());
                props.Write("isError", isError);
            });
        }

        public CallToolResult(IJsonObject jsonObject)
        {
            JsonObject = jsonObject;

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

        public override IJsonObject JsonObject { get; }
        public Content[] Content { get; }
        public bool IsError { get; }
    }
}