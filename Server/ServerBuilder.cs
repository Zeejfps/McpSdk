using System;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server
{
    public sealed class ServerBuilder
    {
        private readonly IJson _json;

        private string _name;
        private string _version;
        private ITransportFactory _transportFactory;
        private IToolsController _toolsController;
        private ILogger _logger;

        public ServerBuilder(IJson json)
        {
            _json = json;
        }

        public ServerBuilder WithLogger(ILogger logger)
        {
            _logger = logger;
            return this;
        }

        public ServerBuilder WithStdioTransport()
        {
            _transportFactory = new StdioTransportFactory(_json);
            return this;
        }

        public ServerBuilder WithSseTransport(ISseServer sseServer, string connectionEndpoint, string messagesEndpoint)
        {
            _transportFactory = new SseTransportFactory(_json, sseServer, connectionEndpoint, messagesEndpoint);
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
        
        public ServerBuilder WithToolsCapability(Action<DefaultToolsController> addTools)
        {
            var toolsController = new DefaultToolsController(_json);
            addTools(toolsController);
            _toolsController = toolsController;
            return this;
        }

        public ServerBuilder WithToolsCapability(IToolsController toolsController)
        {
            _toolsController = toolsController;
            return this;
        }

        public IServer Build()
        {
            var transport = _transportFactory.Create();
            var tools = _toolsController;
            var serverInfo = new ServerInfo(_name, _version);
            var logger = _logger ?? new NullLogger();
            return new McpServer(transport, serverInfo, logger, tools);
        }
    }
}