namespace McpSharp.Protocol
{
    public sealed class JsonRpcNotification
    {
        public string JsonRpcVersion { get; }
        public string Method { get; }

        public JsonRpcNotification(string name)
        {
            JsonRpcVersion = "2.0";
            Method = $"notifications/{name}";
        }
    }
}