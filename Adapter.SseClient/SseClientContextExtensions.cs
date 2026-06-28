using McpSdk.Client;
using McpSdk.Shared;

namespace McpSdk.Adapter.SseClient
{
    public static class SseClientContextExtensions
    {
        /// <summary>
        /// Registers the SSE <see cref="ISseClientFactory"/> for the given endpoint. The
        /// <see cref="ILoggerFactory"/> is resolved from the context.
        /// </summary>
        public static IContext AddSseClient(this IContext context, string baseUrl, string connectionEndpoint)
        {
            return context.AddSingleton<ISseClientFactory>(
                sp => new SseClientFactory(baseUrl, connectionEndpoint, sp.GetRequiredService<ILoggerFactory>()));
        }
    }
}
