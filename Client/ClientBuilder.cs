using System;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Shared;

namespace McpSdk.Client
{
    public sealed class ClientBuilder
    {
        private string _name;
        private string _version;
        private string _title;
        private string _description;
        private readonly DiContainer _container = new DiContainer();

        public ClientBuilder()
        {
            _container.AddSingleton<ILoggerFactory>(new NullLoggerFactory());
        }

        /// <summary>
        /// The service registration surface. Register the transport (and its dependencies) here, e.g.
        /// <c>builder.Context.AddSseTransport()</c>.
        /// </summary>
        public IContext Context => _container;

        /// <summary>Configures <see cref="Context"/> inline while keeping the fluent builder chain.</summary>
        public ClientBuilder ConfigureContext(Action<IContext> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            configure(_container);
            return this;
        }

        public ClientBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        public ClientBuilder WithVersion(string version)
        {
            _version = version;
            return this;
        }

        public ClientBuilder WithTitle(string title)
        {
            _title = title;
            return this;
        }

        public ClientBuilder WithDescription(string description)
        {
            _description = description;
            return this;
        }

        public IClient Build()
        {
            if (_name == null)
                throw new ArgumentNullException(nameof(_name), "Client name cannot be null.");
            
            if (_version == null)
                throw new ArgumentNullException(nameof(_version), "Client version cannot be null.");
            
            var clientInfo = new ClientInfo(_name, _version, _title, _description);

            var provider = _container.BuildServiceProvider();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var transport = provider.GetRequiredService<ITransportFactory>().Create();

            var rootsCapability = provider.GetService<IRootsCapabilityFactory>()?.Create();
            var samplingCapability = provider.GetService<ISamplingCapabilityFactory>()?.Create();
            var elicitationCapability = provider.GetService<IElicitationCapabilityFactory>()?.Create();

            return new McpClient(transport, loggerFactory, clientInfo, rootsCapability, samplingCapability, elicitationCapability);
        }
    }
}