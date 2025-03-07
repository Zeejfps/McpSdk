namespace McpSharp.Client
{
    public interface IHttpClientFactory
    {
        ISseClient Create(string sseEndpoint);
    }
}