namespace McpSharp.Client
{
    public interface IHttpClientFactory
    {
        IHttpClient CreateHttpClient(string sseEndpoint);
    }
}