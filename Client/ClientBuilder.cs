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
        private IRootsCapabilityFactory _rootsCapabilityFactory;
        private ISamplingCapabilityFactory _samplingCapabilityFactory;
        private IElicitationCapabilityFactory _elicitationCapabilityFactory;
        private ILoggerFactory _loggerFactory;
        private readonly DiContainer _container = new DiContainer();

        public ClientBuilder()
        {
            _loggerFactory = new NullLoggerFactory();
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

        public ClientBuilder WithLogger(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            return this;
        }
        
        public ClientBuilder WithRootsCapability(IRootsCapabilityFactory capabilityFactory)
        {
            _rootsCapabilityFactory = capabilityFactory;
            return this;
        }
        
        public ClientBuilder WithSamplingCapability(ISamplingCapabilityFactory capabilityFactory)
        {
            _samplingCapabilityFactory = capabilityFactory;
            return this;
        }

        public ClientBuilder WithElicitationCapability(IElicitationCapabilityFactory capabilityFactory)
        {
            _elicitationCapabilityFactory = capabilityFactory;
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
            var transport = provider.GetRequiredService<ITransportFactory>().Create(_loggerFactory);

            var rootsCapability = _rootsCapabilityFactory?.Create();
            var samplingCapability = _samplingCapabilityFactory?.Create();
            var elicitationCapability = _elicitationCapabilityFactory?.Create();

            var loggerFactory = _loggerFactory;

            return new McpClient(transport, loggerFactory, clientInfo, rootsCapability, samplingCapability, elicitationCapability);
        }
    }
}