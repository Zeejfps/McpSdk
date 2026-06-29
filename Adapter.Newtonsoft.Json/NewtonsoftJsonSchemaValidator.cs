using System.Collections.Generic;
using System.IO;
using McpSdk.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace McpSdk.Adapter.Newtonsoft.Json
{
    public sealed class NewtonsoftJsonSchemaValidator : IJsonSchemaValidator
    {
        public bool IsValid(IJsonObjectWriter value, IJsonObjectWriter schema, out IList<string> errors)
        {
            var valueToken = ToJToken(value);
            var jSchema = JSchema.Parse(Stringify(schema));
            return valueToken.IsValid(jSchema, out errors);
        }

        // Render a write-only object into a JToken. When the value is already backed by a native JToken
        // (the common case — wire-parsed arguments), validate it directly instead of re-serializing.
        private static JToken ToJToken(IJsonObjectWriter writer)
        {
            if (writer is JTokenToJsonObjectAdapter adapter)
                return adapter.Token;

            return JObject.Parse(Stringify(writer));
        }

        private static string Stringify(IJsonObjectWriter writer)
        {
            using var stringWriter = new StringWriter();
            using (var jsonWriter = new JsonTextWriter(stringWriter))
            {
                jsonWriter.WriteStartObject();
                writer.WriteMembers(new JsonTextWriterAdapter(jsonWriter));
                jsonWriter.WriteEndObject();
            }
            return stringWriter.ToString();
        }
    }
}
