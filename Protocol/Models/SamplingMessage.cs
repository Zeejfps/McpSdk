namespace McpSdk.Protocol.Models
{
    public sealed class SamplingMessage
    {
        public string Role { get; }
        public Content Content { get; }
        
        public SamplingMessage(IJsonObject jsonObject)
        {
            Role = jsonObject["role"].AsString();
            
            var contentObj = jsonObject["content"].AsObject();
            Content = Content.Create(contentObj);
        }

        public void ToJson(IJsonWriter writer)
        {
            writer.Write("role", Role);
            writer.Write("content", Content.AsJson);
        }
    }
}