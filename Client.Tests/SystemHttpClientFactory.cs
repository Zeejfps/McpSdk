using McpSharp.Client;

class SystemHttpClientFactory : IHttpClientFactory
{
    public ISseClient Create(string sseEndpoint)
    {
        return new SystemHttpClientAdapter();
    }
}