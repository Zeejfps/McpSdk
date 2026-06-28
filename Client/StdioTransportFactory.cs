using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Client
{
    public sealed class StdioTransportFactory : ITransportFactory
    {
        private readonly IJson _json;
        private readonly ILoggerFactory _loggerFactory;
        private readonly string _command;
        private readonly string _arguments;

        public StdioTransportFactory(IJson json, ILoggerFactory loggerFactory, string command, string[] args)
        {
            _json = json;
            _loggerFactory = loggerFactory;
            _command = command;
            _arguments = string.Join(" ", args);
        }

        public ITransport Create()
        {
            return new StdioTransport(_json, _loggerFactory, _command, _arguments);
        }
    }
}