namespace McpSharp.Protocol.Messages
{
    public sealed class CallToolResultPayload
    {
        public Content[] Content { get; }
        public bool IsError { get; private set; }

        public CallToolResultPayload(Content[] content, bool isError)
        {
            Content = content;
            IsError = isError;
        }
    }
}