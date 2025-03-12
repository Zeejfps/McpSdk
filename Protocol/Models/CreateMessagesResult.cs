namespace McpSdk.Protocol.Models
{
    public sealed class CreateMessagesResult
    {
        public string Role { get; }
        public string Model { get; }
        public string StopReason { get; }
        public Content Content { get; }
        
        public CreateMessagesResult(IJsonObject jsonObject)
        {
            Role = jsonObject["role"]?.AsString();
            Model = jsonObject["module"]?.AsString();
            StopReason = jsonObject["stopReason"]?.AsString();
            
            var contentObj = jsonObject["content"].AsObject();
            Content = Content.Create(contentObj);
        }

        public CreateMessagesResult(string role, string model, Content content, string stopReason)
        {
            Role = role;
            Model = model;
            Content = content;
            StopReason = stopReason;
        }

        public void ToJson(IJsonWriter writer)
        {
            writer.Write("role", Role);
            writer.Write("model", Model);
            writer.Write("content", Content.AsJson);
            writer.Write("stopReason", StopReason);
        }
    }
}