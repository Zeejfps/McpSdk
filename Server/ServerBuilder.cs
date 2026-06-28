using System;
using McpSdk.Protocol.Models;
using McpSdk.Shared;

namespace McpSdk.Server
{
    public sealed class ServerBuilder
    {
        private string _name;
        private string _version;
        private string _title;
        private string _description;
        private readonly DiContainer _container = new DiContainer();

        public ServerBuilder()
        {
            _container.AddSingleton<ILoggerFactory>(new NullLoggerFactory());
        }

        /// <summary>
        /// The service registration surface. Register the transport (and its dependencies) here, e.g.
        /// <c>builder.Context.AddSseTransport()</c>.
        /// </summary>
        public IContext Context => _container;

        /// <summary>Configures <see cref="Context"/> inline while keeping the fluent builder chain.</summary>
        public ServerBuilder ConfigureContext(Action<IContext> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            configure(_container);
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

        public ServerBuilder WithTitle(string title)
        {
            _title = title;
            return this;
        }

        public ServerBuilder WithDescription(string description)
        {
            _description = description;
            return this;
        }

        public IServer Build()
        {
            if (_name == null)
                throw new ArgumentNullException(nameof(_name), "Server name cannot be null.");
            
            if (_version == null)
                throw new ArgumentNullException(nameof(_version), "Server version cannot be null.");
            
            var provider = _container.BuildServiceProvider();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var transport = provider.GetRequiredService<ITransportFactory>().Create();
            var serverInfo = new ServerInfo(_name, _version, _title, _description);

            var tools = provider.GetService<IToolsController>();
            var prompts = provider.GetService<IPromptController>();
            var resources = provider.GetService<IResourcesController>();

            var server =  new McpServer(
                transport,
                serverInfo, 
                loggerFactory,
                tools, 
                prompts,
                resources
            );

            return server;
        }
    }
}