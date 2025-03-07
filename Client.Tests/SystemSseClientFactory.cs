using McpSharp.Client;

class SystemSseClientFactory : ISseClientFactory
{
    public ISseClient Create(string sseEndpoint)
    {
        return new SystemHttpClientAdapter();
    }
}