using McpSharp.Client;

namespace McpSharp.Adapter.SseClient
{
    public class SseClientFactory : ISseClientFactory
    {
        public ISseClient Create()
        {
            return new SseClient();
        }
    }
}