using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using McpSdk.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace McpSdk.Adapter.Newtonsoft.Json
{
    internal sealed class JTokenToJsonObjectAdapter : IJsonObject
    {
        private readonly JToken _jToken;
        
        public JTokenToJsonObjectAdapter(JToken jToken)
        {
            _jToken = jToken;
        }

        /// <summary>The underlying token, read by <see cref="CompiledJsonSchema"/> to validate this instance.</summary>
        internal JToken Token => _jToken;

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

        public void WriteMembers(IJsonWriter writer)
        {
            foreach (var kvp in this)
            {
                writer.Write(kvp.Key, kvp.Value);
            }
        }
    }
}