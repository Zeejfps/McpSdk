using McpSharp.Client;

class SystemHttpClientFactory : IHttpClientFactory
{
    public IHttpClient CreateHttpClient(string sseEndpoint)
    {
        var client = new HttpClient();
        return new SystemHttpClientAdapter(client);
    }
}