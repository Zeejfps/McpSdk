using System.Linq;

namespace McpSdk.Protocol.Models
{
    public sealed class CreateMessageRequest : IJsonObjectWriter
    {
        public CreateMessageRequest(IJsonObject jsonObject)
        {
            Messages = jsonObject["messages"]
                .AsObjectArray()
                .Select(message => new SamplingMessage(message))
                .ToArray();
            
            Preferences = new ModelPreferences(jsonObject);
            SystemPrompt = jsonObject["systemPrompt"]?.AsString();    
            MaxTokens = jsonObject["maxTokens"]?.AsInt();
        }

        public void WriteMembers(IJsonWriter writer)
        {
            writer.Write("messages", Messages);
            writer.Write("modelPreferences", Preferences);
            writer.Write("systemPrompt", SystemPrompt);
            if (MaxTokens.HasValue)
                writer.Write("maxTokens", MaxTokens.Value);
        }
        
        public ModelPreferences Preferences { get; }
        public SamplingMessage[] Messages { get; }
        public string SystemPrompt { get; }
        public int? MaxTokens { get; }
    }
}