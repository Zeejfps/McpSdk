using System;
using System.Linq;

namespace McpSdk.Protocol.Models
{
    public sealed class CreateMessageRequest
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

        public void ToJson(IJsonWriter writer)
        {
            writer.Write("messages", Messages
                .Select<SamplingMessage, Action<IJsonWriter>>(message => message.ToJson)
                .ToArray());
            writer.Write("modelPreferences", Preferences.ToJson);
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