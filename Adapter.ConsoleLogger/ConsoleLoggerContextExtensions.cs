using McpSdk.Adapter.ConsoleLogger;
using McpSdk.Shared;

// The server and client console loggers differ: the server factory must write to stderr only (the stdio
// transport owns stdout), while the client factory may use stdout. A single AddConsoleLogger(IContext)
// cannot tell a server build from a client build, so the registration is split into two namespace-scoped
// overloads with identical signatures. A server app does `using McpSdk.Server;` (alongside ServerBuilder)
// and gets the ServerConsoleLoggerFactory; a client app does `using McpSdk.Client;` (alongside
// ClientBuilder) and gets the ClientConsoleLoggerFactory. The caller's existing builder `using` selects the
// correct factory with no extra ceremony, and the two are never imported together in real code.

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
