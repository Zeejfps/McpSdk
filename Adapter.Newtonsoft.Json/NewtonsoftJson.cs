using System;
using System.IO;
using System.Linq;
using McpSharp.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Adapter.Newtonsoft.Json
{
    public sealed class NewtonsoftJson : IJson
    {
        public IJsonObject Build(Action<IJsonWriter> props)
        {
            throw new NotImplementedException();
        }

        public IJsonObject Parse(string text)
        {
            var jObject = JObject.Parse(text);
            return new JTokenToJsonObjectAdapter(jObject);
        }

        public string Stringify(Action<IJsonWriter> json)
        {
            using var stringWriter = new StringWriter();
            using var jsonTextWriter = new JsonTextWriter(stringWriter);
            jsonTextWriter.WriteStartObject();
            
            jsonTextWriter.WriteEndObject();
            jsonTextWriter.Flush();
            return stringWriter.ToString();
        }
    }

    internal sealed class JTokenToJsonObjectAdapter : IJsonObject
    {
        private readonly JToken _jToken;
        
        public JTokenToJsonObjectAdapter(JToken jToken)
        {
            _jToken = jToken;
        }

        public IJsonProperty this[string propertyName]
        {
            get
            {
                var property = _jToken[propertyName];
                if (property == null)
                    return null;

                return new JTokenToJsonPropertyAdapter(property);
            }
        }
    }

    internal sealed class JTokenToJsonPropertyAdapter : IJsonProperty
    {
        private readonly JToken _jToken;
        
        public JTokenToJsonPropertyAdapter(JToken token)
        {
            _jToken = token;
        }

        public string AsString()
        {
            return _jToken.Value<string>();
        }

        public string[] AsStringArray()
        {
            return _jToken.Value<string[]>();
        }

        public double AsDouble()
        {
            return _jToken.Value<double>();
        }

        public double[] AsDoubleArray()
        {
            return _jToken.Value<double[]>();
        }

        public int AsInt()
        {
            return _jToken.Value<int>();
        }

        public int[] AsIntArray()
        {
            return _jToken.Value<int[]>();
        }

        public float AsFloat()
        {
            return _jToken.Value<float>();
        }

        public float[] AsFloatArray()
        {
            return _jToken.Value<float[]>();
        }

        public bool AsBool()
        {
            return _jToken.Value<bool>();
        }

        public bool[] AsBoolArray()
        {
            return _jToken.Value<bool[]>();
        }

        public IJsonObject AsObject()
        {
            return new JTokenToJsonObjectAdapter(_jToken);
        }

        public IJsonObject[] AsObjectArray()
        {
            var array = _jToken as JArray;
            if (array == null)
                return null;

            return array
                .Select(item => new JTokenToJsonObjectAdapter(item))
                .ToArray<IJsonObject>();
        }
    }
}