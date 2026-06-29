using System.Collections.Generic;
using System.IO;
using McpSdk.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace McpSdk.Adapter.Newtonsoft.Json
{
    public sealed class NewtonsoftJsonSchemaCompiler : IJsonSchemaCompiler
    {
        public IJsonSchema Compile(IJsonObjectWriter schema)
            => new CompiledSchema(JSchema.Parse(Stringify(schema)));

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

        // Parsed once, validated against many values. Read-only over the JSchema, so safe to share.
        private sealed class CompiledSchema : IJsonSchema
        {
            private readonly JSchema _schema;

            public CompiledSchema(JSchema schema) => _schema = schema;

            public bool Validate(IJsonObjectWriter value, out IList<string> errors)
                => ToJToken(value).IsValid(_schema, out errors);

            // When the value is already backed by a native JToken (the common case — wire-parsed
            // arguments), validate it directly instead of re-serializing.
            private static JToken ToJToken(IJsonObjectWriter writer)
            {
                if (writer is JTokenToJsonObjectAdapter adapter)
                    return adapter.Token;
                return JObject.Parse(Stringify(writer));
            }
        }
    }
}
