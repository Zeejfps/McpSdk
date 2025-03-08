namespace McpSharp.Protocol
{
    public sealed class JsonNumberSchema : JsonSchema
    {
        public override string Type => "number";
        public float? Minimum { get; set; }
        public float? Maximum { get; set; }
    }
}