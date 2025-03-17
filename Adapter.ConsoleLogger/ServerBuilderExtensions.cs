using McpSdk.Server;

namespace McpSdk.Adapter.ConsoleLogger
{
    public static class ServerBuilderExtensions
    {
        public static ServerBuilder WithConsoleLogger(this ServerBuilder builder)
        {
            var factory = new ServerConsoleLoggerFactory();
            builder.WithLogger(factory);
            return builder;
        }
    }
}