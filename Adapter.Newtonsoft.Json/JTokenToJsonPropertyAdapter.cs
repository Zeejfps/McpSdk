using System.Linq;
using McpSdk.Protocol;
using Newtonsoft.Json.Linq;

namespace McpSdk.Adapter.Newtonsoft.Json
{
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