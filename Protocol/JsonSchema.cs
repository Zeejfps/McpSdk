namespace McpSdk.Protocol
{
    public abstract class JsonSchema
    {
        public abstract string Type { get; }
        public string Description { get; set; }
    }
}