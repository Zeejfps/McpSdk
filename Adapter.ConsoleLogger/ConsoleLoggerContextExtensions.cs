using McpSdk.Adapter.ConsoleLogger;
using McpSdk.Shared;

namespace McpSdk.Server
{
    /// <summary>
    /// Registration helper that contributes the server console <see cref="ILoggerFactory"/> to an
    /// <see cref="IContext"/>. Selected by a server-side <c>using McpSdk.Server;</c>.
    /// </summary>
    public static class ConsoleLoggerContextExtensions
    {
        /// <summary>
        /// Registers <see cref="ServerConsoleLoggerFactory"/> (stderr-only) as the singleton
        /// <see cref="ILoggerFactory"/>, overriding the default <see cref="NullLoggerFactory"/>.
        /// </summary>
        public static IContext AddConsoleLogger(this IContext context)
            => context.AddSingleton<ILoggerFactory>(new ServerConsoleLoggerFactory());
    }
}

namespace McpSdk.Client
{
    /// <summary>
    /// Registration helper that contributes the client console <see cref="ILoggerFactory"/> to an
    /// <see cref="IContext"/>. Selected by a client-side <c>using McpSdk.Client;</c>.
    /// </summary>
    public static class ConsoleLoggerContextExtensions
    {
        /// <summary>
        /// Registers <see cref="ClientConsoleLoggerFactory"/> as the singleton <see cref="ILoggerFactory"/>,
        /// overriding the default <see cref="NullLoggerFactory"/>.
        /// </summary>
        public static IContext AddConsoleLogger(this IContext context)
            => context.AddSingleton<ILoggerFactory>(new ClientConsoleLoggerFactory());
    }
}
