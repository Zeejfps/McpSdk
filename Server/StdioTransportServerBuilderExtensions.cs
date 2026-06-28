using McpSdk.Protocol;

namespace McpSdk.Server
{
    public static class StdioTransportServerBuilderExtensions
    {
        public static ServerBuilder WithStdioTransport(this ServerBuilder builder, IJson json)
        {
            var factory = new StdioTransportFactory(json);
            builder.WithTransport(factory);
            return builder;
        }
    }
}
