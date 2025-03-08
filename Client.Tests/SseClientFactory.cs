using McpSharp.Client;

class SseClientFactory : ISseClientFactory
{
    public ISseClient Create()
    {
        return new SseClient();
    }
}