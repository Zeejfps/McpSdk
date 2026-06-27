namespace McpSdk.Protocol.Models
{
    public sealed class CreateMessagesResult : IJsonObjectWriter
    {
        public string Role { get; }
        public string Model { get; }
        public string StopReason { get; }

        /// <summary>
        /// The generated content. A text completion carries a single block; a tool-use turn
        /// (<c>stopReason: "toolUse"</c>) carries one or more <see cref="ToolUseContent"/> blocks
        /// (2025-11-25, SEP-1577).
        /// </summary>
        public Content[] Content { get; }

        public CreateMessagesResult(IJsonObject jsonObject)
        {
            Role = jsonObject["role"]?.AsString();
            Model = jsonObject["model"]?.AsString();
            StopReason = jsonObject["stopReason"]?.AsString();
            Content = Models.Content.CreateMany(jsonObject["content"]);
        }

        public CreateMessagesResult(string role, string model, Content content, string stopReason)
            : this(role, model, new[] { content }, stopReason)
        {
        }

        public CreateMessagesResult(string role, string model, Content[] content, string stopReason)
        {
            Role = role;
            Model = model;
            Content = content;
            StopReason = stopReason;
        }

        public void WriteMembers(IJsonWriter writer)
        {
            writer.Write("role", Role);
            writer.Write("model", Model);

            // A single block stays a single object (back-compat with text-only peers); multiple
            // tool-use blocks are emitted as an array.
            if (Content != null && Content.Length == 1)
                writer.Write("content", Content[0]);
            else
                writer.Write("content", Content);

            writer.Write("stopReason", StopReason);
        }
    }
}
