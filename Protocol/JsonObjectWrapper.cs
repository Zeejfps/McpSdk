namespace McpSharp.Protocol
{
    public abstract class JsonObjectWrapper
    {
        protected JsonObjectWrapper(IJsonObject jsonObject)
        {
            JsonObject = jsonObject;
        }

        public IJsonObject JsonObject { get; }

        public override string ToString()
        {
            return JsonObject.ToString();
        }
    }
}