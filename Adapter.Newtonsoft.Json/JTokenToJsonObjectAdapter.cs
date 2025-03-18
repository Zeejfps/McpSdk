using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using McpSdk.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace McpSdk.Adapter.Newtonsoft.Json
{
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

        public IEnumerator<KeyValuePair<string, IJsonProperty>> GetEnumerator()
        {
            var obj = (JObject)_jToken;
            return obj
                .Properties()
                .Select(prop => new KeyValuePair<string, IJsonProperty>(prop.Name, new JTokenToJsonPropertyAdapter(prop.Value)))
                .GetEnumerator();
        }

        public override string ToString()
        {
            return _jToken.ToString(Formatting.None);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool IsValid(IJsonObject schema, out IList<string> errors)
        {
            var jSchema = JSchema.Parse(schema.ToString());
            return _jToken.IsValid(jSchema, out errors);
        }

        public void AsJson(IJsonWriter writer)
        {
            foreach (var kvp in this)
            {
                writer.Write(kvp.Key, kvp.Value);
            }
        }
    }
}