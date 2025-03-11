using System;
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
            _writer.WriteStartObject();
            _writer.WriteRawValue(obj.ToString());
            _writer.WriteEndObject();
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

        public IJsonWriter Write(string propertyName, Action<IJsonWriter> obj)
        {
            _writer.WritePropertyName(propertyName);
            _writer.WriteStartObject();
            obj(this);
            _writer.WriteEndObject();
            return this;
        }

        public IJsonWriter Write(string propertyName, Action<IJsonWriter>[] objs)
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
    }
}