namespace McpSharp.Protocol
{
    public sealed class CallToolResultPayload
    {
        public Content Content { get; }
        public bool IsError { get; private set; }
    }
}