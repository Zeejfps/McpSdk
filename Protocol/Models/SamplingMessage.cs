namespace McpSdk.Protocol.Models
{
    public sealed class SamplingMessage : IJsonSerializable
    {
        public string Role { get; }
        public Content Content { get; }
        
        public SamplingMessage(IJsonObject jsonObject)
        {
            Role = jsonObject["role"].AsString();
            
            var contentObj = jsonObject["content"].AsObject();
            Content = Content.Create(contentObj);
        }

        public void AsJson(IJsonWriter writer)
        {
            writer.Write("role", Role);
            writer.Write("content", Content);
        }
    }
}