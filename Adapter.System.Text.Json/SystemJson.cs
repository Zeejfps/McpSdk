using System;
using System.IO;
using System.Text;
using System.Text.Json;
using McpSdk.Protocol;

namespace McpSdk.Adapter.System.Text.Json
{
    public sealed class SystemJson : IJson
    {
        public IJsonObject Parse(string text)
        {
            var document = JsonDocument.Parse(text);
            return new JsonElementToJsonObjectAdapter(document.RootElement);
        }

        public string Stringify(Action<IJsonWriter> json)
        {
            using var memory = new MemoryStream();
            using var writer = new Utf8JsonWriter(memory);
            writer.WriteStartObject();
            json(new JsonWriter(writer));
            writer.WriteEndObject();
            writer.Flush();
            var jsonString = Encoding.UTF8.GetString(memory.ToArray());
            return jsonString;
        }
    }
}