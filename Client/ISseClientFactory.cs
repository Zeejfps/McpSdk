namespace McpSharp.Client
{
    public interface ISseClientFactory
    {
        ISseClient Create(string sseEndpoint);
    }
}