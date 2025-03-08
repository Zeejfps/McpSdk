using System.Text;

namespace McpSharp.Protocol
{
    public sealed class JsonNumberSchema : JsonSchema
    {
        public override string Type => "number";
        public float? Minimum { get; set; }
        public float? Maximum { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            
            sb.Append('{');
            
            sb.Append($"\"type\": ").Append('"').Append(Type).Append('"');

            if (!string.IsNullOrEmpty(Description))
            {
                sb.Append(", \"description\": ").Append('"').Append(Description).Append('"');
            }
            
            sb.Append('}');
            
            return sb.ToString();
        }
    }
}