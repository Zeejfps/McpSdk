using System.Linq;

namespace McpSdk.Protocol
{
    public sealed class CreateMessageArguments : JsonObjectWrapper
    {
        public CreateMessageArguments(IJsonObject jsonObject)
        {
            Messages = jsonObject["messages"]
                .AsObjectArray()
                .Select(message => new SamplingMessage(message))
                .ToArray();
            
            Preferences = new ModelPreferences(jsonObject);
            SystemPrompt = jsonObject["systemPrompt"]?.AsString();    
            MaxTokens = jsonObject["maxTokens"]?.AsInt();
            JsonObject = jsonObject;
        }
        
        public ModelPreferences Preferences { get; }
        public SamplingMessage[] Messages { get; }
        public string SystemPrompt { get; }
        public int? MaxTokens { get; }
        public override IJsonObject JsonObject { get; }
    }
}