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

            Tools = jsonObject["tools"]?
                .AsObjectArray()
                .Select(tool => new Tool(tool))
                .ToArray();

            var toolChoice = jsonObject["toolChoice"]?.AsObject();
            if (toolChoice != null)
                ToolChoice = new ToolChoice(toolChoice);
        }

        public CreateMessageRequest(
            SamplingMessage[] messages,
            int? maxTokens = null,
            string systemPrompt = null,
            Tool[] tools = null,
            ToolChoice toolChoice = null,
            ModelPreferences preferences = null)
        {
            Messages = messages;
            MaxTokens = maxTokens;
            SystemPrompt = systemPrompt;
            Tools = tools;
            ToolChoice = toolChoice;
            Preferences = preferences;
        }

        public void WriteMembers(IJsonWriter writer)
        {
            Messages.WriteTo(writer, "messages");
            if (Preferences != null)
                writer.Write("modelPreferences", Preferences);
            writer.Write("systemPrompt", SystemPrompt);
            if (MaxTokens.HasValue)
                writer.Write("maxTokens", MaxTokens.Value);

            // Sampling-with-tools (2025-11-25, SEP-1577): advertised only when the server supplies them.
            if (Tools is { Length: > 0 })
                Tools.WriteTo(writer, "tools");
            if (ToolChoice != null)
                writer.Write("toolChoice", ToolChoice);
        }

        public ModelPreferences Preferences { get; }
        public SamplingMessage[] Messages { get; }
        public string SystemPrompt { get; }
        public int? MaxTokens { get; }

        /// <summary>Optional tools the model may call during sampling (2025-11-25).</summary>
        public Tool[] Tools { get; }

        /// <summary>Optional control over whether the model may, must, or must not call tools.</summary>
        public ToolChoice ToolChoice { get; }
    }
}
