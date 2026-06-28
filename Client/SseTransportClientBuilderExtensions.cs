using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Client
{
    public static class SseTransportContextExtensions
    {
        /// <summary>
        /// Registers the SSE client transport. Requires <see cref="IJson"/> and <see cref="ISseClientFactory"/>
        /// to already be registered in the context — they are injected into <see cref="SseTransportFactory"/>.
        /// </summary>
        public static IContext AddSseTransport(this IContext context)
        {
            return context.AddSingleton<ITransportFactory, SseTransportFactory>();
        }
    }
}
