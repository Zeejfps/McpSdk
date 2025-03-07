using McpSharp.Client;

class SystemHttpClientFactory : IHttpClientFactory
{
    private readonly IJson _json;

    public SystemHttpClientFactory(IJson json)
    {
        _json = json;
    }

    public IHttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        return new SystemHttpClientAdapter(_json, client);
    }
}