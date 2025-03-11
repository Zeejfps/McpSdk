using System.Linq;

namespace McpSharp.Protocol
{
    public sealed class CreateMessageParams : JsonObjectWrapper
    {
        public CreateMessageParams(IJsonObject jsonObject) : base(jsonObject)
        {
            Messages = jsonObject["messages"]
                .AsObjectArray()
                .Select(message => new SamplingMessage(message))
                .ToArray();
            
            Preferences = new ModelPreferences(jsonObject);
            SystemPrompt = jsonObject["systemPrompt"]?.AsString();    
            MaxTokens = jsonObject["maxTokens"]?.AsInt();
        }
        
        public ModelPreferences Preferences { get; }
        public SamplingMessage[] Messages { get; }
        public string SystemPrompt { get; }
        public int? MaxTokens { get; }
    }
}