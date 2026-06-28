namespace McpSdk.Shared
{
    /// <summary>Logging registration helpers for <see cref="IContext"/>.</summary>
    public static class LoggingContextExtensions
    {
        /// <summary>
        /// Registers the <see cref="ILoggerFactory"/> used by the server/client and its transport.
        /// Overrides the default <see cref="NullLoggerFactory"/> seeded by the builder.
        /// </summary>
        public static IContext AddLogger(this IContext context, ILoggerFactory loggerFactory)
            => context.AddSingleton<ILoggerFactory>(loggerFactory);
    }
}
