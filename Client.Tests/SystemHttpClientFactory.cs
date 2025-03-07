using McpSharp.Client;

class SystemHttpClientFactory : IHttpClientFactory
{
    public async Task<IHttpClient> CreateHttpClient(string sseEndpoint)
    {
        var client = new HttpClient();
        return new SystemHttpClientAdapter(client);
    }
}