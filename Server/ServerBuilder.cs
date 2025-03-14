using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Shared;

namespace McpSdk.Server
{
    public sealed class ServerBuilder
    {
        public IJson Json { get; }

        private string _name;
        private string _version;
        private ITransportFactory _transportFactory;
        private IToolsController _toolsController;
        private IPromptController _promptsController;
        private IResourcesController _resourcesController;
        private ILoggerFactory _loggerFactory;

        public ServerBuilder(IJson json)
        {
            Json = json;
            _loggerFactory = new NullLoggerFactory();
        }
        
        public ServerBuilder WithLogger(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            return this;
        }

        public ServerBuilder WithTransport(ITransportFactory transportFactory)
        {
            _transportFactory = transportFactory;
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

        public ServerBuilder WithResourcesCapability(IResourcesController resourcesController)
        {
            _resourcesController = resourcesController;
            return this;
        }
        
        public ServerBuilder WithPromptsCapability(IPromptController promptsController)
        {
            _promptsController = promptsController;
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
            var prompts = _promptsController;
            
            var resources = _resourcesController;
            var server =  new McpServer(
                transport,
                serverInfo, 
                loggerFactory,
                tools, 
                prompts,
                resources
            );
            
            // NOTE(Zee): Potential change
            // if (prompts != null)
            // {
            //     server.RegisterHandler("tools/list", (requestId, IJsonObject arguments) =>
            //     {
            //         var result = await prompts.ListPrompts();
            //         await transport.SendOkResponse(requestId, result.AsJson);
            //     });
            // }

            return server;
        }
    }
}