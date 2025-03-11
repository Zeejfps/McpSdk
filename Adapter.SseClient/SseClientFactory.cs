using McpSdk.Client;

namespace McpSdk.Adapter.SseClient
{
    public class SseClientFactory : ISseClientFactory
    {
        public ISseClient Create()
        {
            return new SseClient();
        }
    }
}