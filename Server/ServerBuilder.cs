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
        private ILoggerFactory _loggerFactory;

        public ServerBuilder(IJson json)
        {
            _json = json;
            _loggerFactory = new NullLoggerFactory();
        }

        public ServerBuilder WithLogger(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
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
            var loggerFactory = _loggerFactory;
            var transport = _transportFactory.Create(loggerFactory);
            var serverInfo = new ServerInfo(_name, _version);
            var tools = _toolsController;
            return new McpServer(transport, serverInfo, loggerFactory, tools);
        }
    }
}