namespace McpSdk.Protocol.Models
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
            Content = Content.Create(contentObj);
        }

        public CreateMessagesResult(IJson json, string role, string model, Content content, string stopReason)
        {
            Role = role;
            Model = model;
            Content = content;
            StopReason = stopReason;
            JsonObject = json.Object(props =>
            {
                props.Write("role", role);
                props.Write("model", model);
                props.Write("content", content.ToJson);
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