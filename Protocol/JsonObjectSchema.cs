using System.Collections.Generic;
using System.Text;

namespace McpSdk.Protocol
{
    public sealed class JsonObjectSchema : JsonSchema
    {
        public override string Type => "object";
        public Dictionary<string, JsonSchema> Properties { get; set; }
        public string[] Required { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"type\":\"{Type}\"");
            if (Properties != null && Properties.Count > 0)
            {
                sb.Append(", properties: {");
                var index = 0;
                foreach (var kvp in Properties)
                {
                    var key = kvp.Key;
                    var value = kvp.Value;
                    sb.Append($"\"{key}\": \"{value}\"");
                    index++;
                    if (index < Properties.Count)
                    {
                        sb.Append(", ");
                    }
                }
                sb.Append("}");
            }

            if (Required != null && Required.Length > 0)
            {
                sb.Append(", required:[");
                for (var i = 0; i < Required.Length - 1; i++)
                {
                    var requiredProperty = Required[i];
                    sb.Append($"\"{requiredProperty}\",");
                }
                var lastRequiredProperty = Required[Required.Length - 1];
                sb.Append($"\"{lastRequiredProperty}\"");
                sb.Append("]");
            }

            if (!string.IsNullOrEmpty(Description))
            {
                sb.Append(", \"description\": ").Append('"').Append(Description).Append('"');
            }
            
            sb.Append("}");
            return sb.ToString();
        }
    }
}