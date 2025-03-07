using McpSharp.Client;

class SystemSseClientFactory : ISseClientFactory
{
    public ISseClient Create()
    {
        return new SystemHttpClientAdapter();
    }
}