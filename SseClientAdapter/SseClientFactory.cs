using McpSharp.Client;

namespace SseClientAdapter;

public class SseClientFactory : ISseClientFactory
{
    public ISseClient Create()
    {
        return new SseClient();
    }
}