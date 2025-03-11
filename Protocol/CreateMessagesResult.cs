namespace McpSharp.Protocol
{
    public sealed class CreateMessagesResult : JsonObjectWrapper
    {
        public CreateMessagesResult(IJsonObject jsonObject)
        {
            JsonObject = jsonObject;
            Role = jsonObject["role"]?.AsString();
            Model = jsonObject["module"]?.AsString();
            StopReason = jsonObject["stopReason"]?.AsString();
            
            var contentObj = jsonObject["content"].AsObject();
            var type = contentObj["type"].AsString();
            if (type == "text")
            {
                Content = new TextContent(contentObj);
            }
            else if (type == "image")
            {
                Content = new ImageContent(contentObj);
            }
            else if (type == "resource")
            {
                Content = new ResourceContent(contentObj);
            }
            else
            {
                Content = new UnknownContent(contentObj);
            }
        }

        public CreateMessagesResult(IJson json, string role, string model, Content content, string stopReason)
        {
            Role = role;
            Model = model;
            Content = content;
            StopReason = stopReason;
            JsonObject = json.Build(props =>
            {
                props.Write("role", role);
                props.Write("model", model);
                props.Write("content", content.JsonObject);
                props.Write("stopReason", stopReason);
            });
        }

        public string Role { get; }
        public string Model { get; }
        public string StopReason { get; }
        public Content Content { get; }

        public override IJsonObject JsonObject { get; }
    }
}