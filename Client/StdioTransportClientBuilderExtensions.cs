using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Client
{
    public static class StdioTransportContextExtensions
    {
        /// <summary>
        /// Registers the stdio client transport that spawns <paramref name="command"/>. <see cref="IJson"/>
        /// is injected from the context; the command and arguments are captured here because primitive
        /// parameters cannot be resolved by the container.
        /// </summary>
        public static IContext AddStdioTransport(this IContext context, string command, string[] args)
        {
            return context.AddSingleton<ITransportFactory>(
                sp => new StdioTransportFactory(sp.GetRequiredService<IJson>(), command, args));
        }
    }
}
