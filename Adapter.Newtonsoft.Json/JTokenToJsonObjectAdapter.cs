using McpSdk.Protocol;
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
    }
}