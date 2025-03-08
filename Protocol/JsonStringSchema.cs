namespace McpSharp.Protocol
{
    public sealed class JsonStringSchema : JsonSchema
    {
        public override string Type => "string";
        public int MinLength { get; set; }
        public int MaxLength { get; set; }
    }
}