using System.Collections.Generic;

namespace McpSharp.Protocol
{
    public sealed class JsonObjectSchema : JsonSchema
    {
        public override string Type => "object";
        public Dictionary<string, JsonSchema> Properties { get; set; }
        public string[] Required { get; set; }
    }
}