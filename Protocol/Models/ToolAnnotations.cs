namespace McpSdk.Protocol.Models
{
    /// <summary>
    /// Optional behavioural hints a tool advertises about itself (2025-03-26+). These are untrusted
    /// hints, not guarantees — a client must not make security decisions based on them. Every field is
    /// optional; spec defaults are noted but only emitted when explicitly set.
    /// </summary>
    public sealed class ToolAnnotations : IJsonSerializable
    {
        /// <summary>Human-friendly display title for the tool.</summary>
        public string Title { get; set; }

        /// <summary>If true, the tool does not modify its environment (default false).</summary>
        public bool? ReadOnlyHint { get; set; }

        /// <summary>If true, the tool may perform destructive updates (default true). Meaningful only when not read-only.</summary>
        public bool? DestructiveHint { get; set; }

        /// <summary>If true, repeated calls with the same args have no additional effect (default false). Meaningful only when not read-only.</summary>
        public bool? IdempotentHint { get; set; }

        /// <summary>If true, the tool interacts with an open, external world (default true).</summary>
        public bool? OpenWorldHint { get; set; }

        public ToolAnnotations() {}

        public ToolAnnotations(IJsonObject jsonObject)
        {
            Title = jsonObject["title"]?.AsString();
            ReadOnlyHint = jsonObject["readOnlyHint"]?.AsBool();
            DestructiveHint = jsonObject["destructiveHint"]?.AsBool();
            IdempotentHint = jsonObject["idempotentHint"]?.AsBool();
            OpenWorldHint = jsonObject["openWorldHint"]?.AsBool();
        }

        public void AsJson(IJsonWriter writer)
        {
            if (Title != null)
                writer.Write("title", Title);
            if (ReadOnlyHint.HasValue)
                writer.Write("readOnlyHint", ReadOnlyHint.Value);
            if (DestructiveHint.HasValue)
                writer.Write("destructiveHint", DestructiveHint.Value);
            if (IdempotentHint.HasValue)
                writer.Write("idempotentHint", IdempotentHint.Value);
            if (OpenWorldHint.HasValue)
                writer.Write("openWorldHint", OpenWorldHint.Value);
        }
    }
}
