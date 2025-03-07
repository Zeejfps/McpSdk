using McpSharp.Client;

class SystemHttpClientFactory : IHttpClientFactory
{
    public ISseClient Create(string sseEndpoint)
    {
        var client = new HttpClient();
        return new SystemHttpClientAdapter(client);
    }
}