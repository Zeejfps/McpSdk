using McpSharp.Protocol;

namespace McpSharp.Client
{
    internal sealed class StdioTransportFactory : ITransportFactory
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

        public ITransport Create()
        {
            return new StdioTransport(_json, _command, _arguments);
        }
    }
}