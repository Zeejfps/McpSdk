using System;
using McpSdk.Protocol;

namespace McpSdk.Server
{
    public sealed class ServerBuilder
    {
        private readonly IJson _json;

        private string _name;
        private string _version;
        private ITransportFactory _transportFactory;
        private IToolsCapability _toolsCapability;

        public ServerBuilder(IJson json)
        {
            _json = json;
        }

        public ServerBuilder WithStdioTransport()
        {
            _transportFactory = new StdioTransportFactory(_json);
            return this;
        }

        public ServerBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        public ServerBuilder WithVersion(string version)
        {
            _version = version;
            return this;
        }

        public ServerBuilder WithToolsCapability(IToolsCapability toolsCapability)
        {
            _toolsCapability = toolsCapability;
            return this;
        }

        public IServer Build()
        {
            var transport = _transportFactory.Create();
            var tools = _toolsCapability;
            return new McpServer(transport, tools);
        }
    }
}