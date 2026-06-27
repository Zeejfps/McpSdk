using System.Collections.Generic;
using McpSdk.Protocol;
using Newtonsoft.Json;

namespace McpSdk.Adapter.Newtonsoft.Json
{
    internal sealed class JsonTextWriterAdapter : IJsonWriter
    {
        private readonly JsonTextWriter _writer;
        
        public JsonTextWriterAdapter(JsonTextWriter writer)
        {
            _writer = writer;
        }
        
        public IJsonWriter Write(string propertyName, string value)
        {
            _writer.WritePropertyName(propertyName);
            _writer.WriteValue(value);
            return this;
        }

        public IJsonWriter Write(string propertyName, string[] value)
        {
            _writer.WritePropertyName(propertyName);
            _writer.WriteStartArray();
            foreach (var valueItem in value)
            {
                _writer.WriteValue(valueItem);
            }
            _writer.WriteEndArray();
            return this;
        }

        public IJsonWriter Write(string propertyName, double value)
        {
            _writer.WritePropertyName(propertyName);
            _writer.WriteValue(value);
            return this;
        }

        public IJsonWriter Write(string propertyName, double[] value)
        {
            _writer.WritePropertyName(propertyName);
            _writer.WriteStartArray();
            foreach (var valueItem in value)
            {
                _writer.WriteValue(valueItem);
            }
            _writer.WriteEndArray();
            return this;
        }

        public IJsonWriter Write(string propertyName, int value)
        {
            _writer.WritePropertyName(propertyName);
            _writer.WriteValue(value);
            return this;
        }

        public IJsonWriter Write(string propertyName, int[] value)
        {
            _writer.WritePropertyName(propertyName);
            _writer.WriteStartArray();
            foreach (var valueItem in value)
            {
                _writer.WriteValue(valueItem);
            }
            _writer.WriteEndArray();
            return this;
        }

        public IJsonWriter Write(string propertyName, long value)
        {
            _writer.WritePropertyName(propertyName);
            _writer.WriteValue(value);
            return this;
        }

        public IJsonWriter Write(string propertyName, float value)
        {
            _writer.WritePropertyName(propertyName);
            _writer.WriteValue(value);
            return this;
        }

        public IJsonWriter Write(string propertyName, float[] value)
        {
            _writer.WritePropertyName(propertyName);
            _writer.WriteStartArray();
            foreach (var valueItem in value)
            {
                _writer.WriteValue(valueItem);
            }
            _writer.WriteEndArray();
            return this;
        }

        public IJsonWriter Write(string propertyName, bool value)
        {
            _writer.WritePropertyName(propertyName);
            _writer.WriteValue(value);
            return this;
        }

        public IJsonWriter Write(string propertyName, bool[] value)
        {
            _writer.WritePropertyName(propertyName);
            _writer.WriteStartArray();
            foreach (var valueItem in value)
            {
                _writer.WriteValue(valueItem);
            }
            _writer.WriteEndArray();
            return this;
        }

        public IJsonWriter Write(string propertyName, IJsonObject obj)
        {
            _writer.WritePropertyName(propertyName);
            _writer.WriteRawValue(obj.ToString());
            return this;
        }

        public IJsonWriter Write(string propertyName, IJsonObject[] objs)
        {
            _writer.WritePropertyName(propertyName);
            _writer.WriteStartArray();
            foreach (var obj in objs)
                _writer.WriteRawValue(obj.ToString());
            _writer.WriteEndArray();
            return this;
        }

        public IJsonWriter Write(string propertyName, Protocol.Json obj)
        {
            _writer.WritePropertyName(propertyName);
            _writer.WriteStartObject();
            obj(this);
            _writer.WriteEndObject();
            return this;
        }

        public IJsonWriter Write(string propertyName, Protocol.Json[] objs)
        {
            _writer.WritePropertyName(propertyName);
            _writer.WriteStartArray();
            foreach (var obj in objs)
            {
                _writer.WriteStartObject();
                obj(this);
                _writer.WriteEndObject();
            }
            _writer.WriteEndArray();
            return this;
        }

        public IJsonWriter Write(string propertyName, IJsonObjectWriter value)
        {
            _writer.WritePropertyName(propertyName);
            _writer.WriteStartObject();
            value.WriteMembers(this);
            _writer.WriteEndObject();
            return this;
        }

        public IJsonWriter Write(string propertyName, IEnumerable<IJsonObjectWriter> values)
        {
            _writer.WritePropertyName(propertyName);
            _writer.WriteStartArray();
            foreach (var value in values)
            {
                _writer.WriteStartObject();
                value.WriteMembers(this);
                _writer.WriteEndObject();
            }
            _writer.WriteEndArray();
            return this;
        }

        public IJsonWriter Write(string propertyName, IJsonProperty property)
        {
            _writer.WritePropertyName(propertyName);
            _writer.WriteRawValue(property.ToString());
            return this;
        }

        public IJsonWriter Write(IJsonObject obj)
        {
            _writer.WriteRawValue(obj.ToString());
            return this;
        }
    }
}