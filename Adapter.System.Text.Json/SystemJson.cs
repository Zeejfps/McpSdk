using System;
using System.IO;
using System.Text;
using System.Text.Json;
using McpSharp.Protocol;

namespace McpSharp.Adapter.System.Text.Json
{
    public sealed class SystemJson : IJson
    {
        public IJsonObject Build(Action<IJsonWriter> props)
        {
            var json = Stringify(props);
            return Parse(json);
        }

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

    sealed class JsonElementToJsonObjectAdapter : IJsonObject
    {
        private readonly JsonElement _element;

        public JsonElementToJsonObjectAdapter(JsonElement _element)
        {
            this._element = _element;
        }

        public IJsonProperty? this[string propertyName]
        {
            get
            {
                var root = _element;
                if (root.TryGetProperty(propertyName, out var element))
                {
                    return new JsonElementToJsonPropertyAdapter(element);
                }
                return null;
            }
        }

        public override string ToString()
        {
            return _element.ToString();
        }
    }
}