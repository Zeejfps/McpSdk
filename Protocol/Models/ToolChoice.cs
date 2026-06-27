namespace McpSdk.Protocol.Models
{
    /// <summary>
    /// Controls whether a sampling request's model may, must, or must not call tools (2025-11-25,
    /// SEP-1577). Carried as <c>{ "mode": "auto" | "required" | "none" }</c>.
    /// </summary>
    public sealed class ToolChoice : IJsonObjectWriter
    {
        /// <summary>The model decides whether to use tools (the default).</summary>
        public const string ModeAuto = "auto";

        /// <summary>The model must use at least one tool before completing.</summary>
        public const string ModeRequired = "required";

        /// <summary>The model must not use any tools.</summary>
        public const string ModeNone = "none";

        public string Mode { get; }

        public ToolChoice(string mode)
        {
            Mode = mode;
        }

        public ToolChoice(IJsonObject jsonObject)
        {
            Mode = jsonObject["mode"]?.AsString();
        }

        public static ToolChoice Auto => new(ModeAuto);
        public static ToolChoice Required => new(ModeRequired);
        public static ToolChoice None => new(ModeNone);

        public void WriteMembers(IJsonWriter writer)
        {
            writer.Write("mode", Mode);
        }
    }
}
