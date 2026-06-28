namespace McpSdk.Protocol.Models
{
    public sealed class SamplingMessage : IJsonObjectWriter
    {
        public string Role { get; }

        /// <summary>
        /// The message content. A plain message carries a single block; a tool-use assistant message or
        /// a tool-result user message carries several (2025-11-25, SEP-1577).
        /// </summary>
        public Content[] Content { get; }

        public SamplingMessage(IJsonObject jsonObject)
        {
            Role = jsonObject["role"].AsString();
            Content = jsonObject["content"].AsArrayOrSingle(Models.Content.FromJsonObject);
        }

        public SamplingMessage(string role, params Content[] content)
        {
            Role = role;
            Content = content;
        }

        public void WriteMembers(IJsonWriter writer)
        {
            writer.Write("role", Role);

            // Emit a single object for the common one-block case, an array otherwise — both are valid
            // per spec, and the single-object form keeps plain messages identical to older peers.
            if (Content != null && Content.Length == 1)
                writer.Write("content", Content[0]);
            else
                Content.WriteTo(writer, "content");
        }
    }
}
