using System.Text;

namespace McpSharp.Protocol
{
    public sealed class JsonStringSchema : JsonSchema
    {
        public override string Type => "string";
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        
        public override string ToString()
        {
            var sb = new StringBuilder();
            
            sb.Append('{');
            
            sb.Append($"\"type\":").Append('"').Append(Type).Append('"');

            if (MinLength.HasValue)
            {
                sb.Append(',').Append("\"minLength\":").Append(MinLength.Value);
            }
            
            if (MaxLength.HasValue)
            {
                sb.Append(',').Append("\"maxLength\":").Append(MaxLength.Value);
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