using McpSdk.Shared;

namespace McpSdk.Adapter.ConsoleLogger
{
    public static class ConsoleLoggerContextExtensions
    {
        /// <summary>Registers the console logger factory as the context's <see cref="ILoggerFactory"/>.</summary>
        public static IContext AddConsoleLogger(this IContext context)
        {
            return context.AddLogger(new ServerConsoleLoggerFactory());
        }
    }
}
