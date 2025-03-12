using System.Collections.Generic;
using McpSdk.Protocol;
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

        public override string ToString()
        {
            return _jToken.ToString();
        }

        public bool IsValid(IJsonObject schema, out IList<string> errors)
        {
            var jSchema = JSchema.Parse(schema.ToString());
            return _jToken.IsValid(jSchema, out errors);
        }
    }
}