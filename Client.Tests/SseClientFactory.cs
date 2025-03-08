using McpSharp.Client;

namespace Client.Tests;

internal class SseClientFactory : ISseClientFactory
{
    public ISseClient Create()
    {
        return new SseClient();
    }
}