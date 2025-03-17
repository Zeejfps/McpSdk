using McpSdk.Protocol;

namespace McpSdk.Client
{
    public static class StdioTransportClientBuilderExtensions
    {
        public static ClientBuilder WithStdioTransport(this ClientBuilder clientBuilder, IJson json, string command, string[] args)
        {
            var factory = new StdioTransportFactory(json, command, args);
            clientBuilder.WithTransport(factory);
            return clientBuilder;
        }
    }
}