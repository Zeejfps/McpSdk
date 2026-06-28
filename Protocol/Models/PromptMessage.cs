namespace McpSdk.Protocol.Models
{
    /// <summary>
    /// One message in a rendered prompt (<c>prompts/get</c>). A <c>role</c> ("user" / "assistant")
    /// paired with a single content block — text, image, audio, resource_link, or an embedded resource.
    /// (Unlike a sampling message, a prompt message carries exactly one block, per spec.) <c>_meta</c>
    /// is the only metadata field a message admits — <c>title</c>/<c>icons</c> live on the content
    /// block or the parent <see cref="Prompt"/>, not on a message.
    /// </summary>
    public sealed class PromptMessage : IJsonObjectWriter
    {
        public string Role { get; set; }
        public Content Content { get; set; }

        /// <summary>Opaque, implementation-defined metadata.</summary>
        public Meta Meta { get; set; }

        public PromptMessage() {}

        public PromptMessage(string role, Content content)
        {
            Role = role;
            Content = content;
        }

        public PromptMessage(IJsonObject jsonObject)
        {
            Role = jsonObject["role"].AsString();

            var contentObj = jsonObject["content"]?.AsObject();
            if (contentObj != null)
                Content = Content.FromJsonObject(contentObj);

            var metaObj = jsonObject["_meta"]?.AsObject();
            if (metaObj != null)
                Meta = new Meta(metaObj);
        }

        public void WriteMembers(IJsonWriter writer)
        {
            writer.Write("role", Role);
            if (Content != null)
                writer.Write("content", Content);
            Meta?.WriteTo(writer, "_meta");
        }
    }
}
