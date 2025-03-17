using McpSdk.Protocol;

namespace McpSdk.Client
{
    public static class SseTransportClientBuilderExtensions
    {
        public static ClientBuilder WithSseTransport(this ClientBuilder builder, IJson json, ISseClientFactory sseClientFactory)
        {
            var sseTransportFactory = new SseTransportFactory(json, sseClientFactory);
            builder.WithTransport(sseTransportFactory);
            return builder;
        }
    }
}