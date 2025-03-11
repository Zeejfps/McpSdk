using System.Text;

namespace McpSdk.Protocol
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
            
            sb.Append($"\"type\":").Append('"').Append(Type).Append('"');

            if (Minimum.HasValue)
            {
                sb.Append(',').Append("\"minimum\":").Append(Minimum.Value);
            }
            
            if (Maximum.HasValue)
            {
                sb.Append(',').Append("\"maximum\":").Append(Maximum.Value);
            }
            
            if (!string.IsNullOrEmpty(Description))
            {
                sb.Append(", \"description\":").Append('"').Append(Description).Append('"');
            }
            
            sb.Append('}');
            
            return sb.ToString();
        }
    }
}