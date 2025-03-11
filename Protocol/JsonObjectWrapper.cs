namespace McpSharp.Protocol
{
    public abstract class JsonObjectWrapper
    {
        public abstract IJsonObject JsonObject { get; }

        public override string ToString()
        {
            return JsonObject.ToString();
        }
    }
}