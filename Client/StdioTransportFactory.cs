using McpSharp.Protocol;

namespace McpSharp.Client
{
    internal sealed class StdioTransportFactory : ITransportFactory
    {
        private readonly string _command;
        private readonly string _arguments;

        public StdioTransportFactory(string command, string[] args)
        {
            _command = command;
            _arguments = string.Join(" ", args);
        }

        public ITransport Create()
        {
            return new StdioTransport(_command, _arguments);
        }
    }
}