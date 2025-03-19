using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Client
{
    public sealed class StdioTransportFactory : ITransportFactory
    {
        private readonly IJson _json;
        private readonly string _command;
        private readonly string _arguments;

        public StdioTransportFactory(IJson json, string command, string[] args)
        {
            _json = json;
            _command = command;
            _arguments = string.Join(" ", args);
        }

        public ITransport Create(ILoggerFactory loggerFactory)
        {
            return new StdioTransport(_json, loggerFactory, _command, _arguments);
        }
    }
}