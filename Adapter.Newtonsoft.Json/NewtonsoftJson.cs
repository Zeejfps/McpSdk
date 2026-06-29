using System;
using System.IO;
using McpSdk.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace McpSdk.Adapter.Newtonsoft.Json
{
    public sealed class NewtonsoftJson : IJson
    {
        public IJsonObject Parse(string text)
        {
            var jObject = JObject.Parse(text);
            return new JTokenToJsonObjectAdapter(jObject);
        }

        public string Stringify(Action<IJsonWriter> json)
        {
            using var stringWriter = new StringWriter();
            using var writer = new JsonTextWriter(stringWriter);
            var writerAdapter = new JsonTextWriterAdapter(writer);
            writer.WriteStartObject();
            json(writerAdapter);
            writer.WriteEndObject();
            writer.Flush();
            return stringWriter.ToString();
        }

        public ICompiledJsonSchema CompileSchema(IJsonObjectWriter schema)
        {
            return new CompiledJsonSchema(JSchema.Parse(Stringify(schema.WriteMembers)));
        }
    }
}