using System;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Shared;

namespace McpSdk.Client
{
    public sealed class ClientBuilder
    {
        private readonly IJson _json;
        
        private string _name;
        private string _version;
        private ITransportFactory _transportFactory;
        private IRootsCapabilityFactory _rootsCapabilityFactory;
        private ISamplingCapabilityFactory _samplingCapabilityFactory;
        private ILoggerFactory _loggerFactory;

        public ClientBuilder(IJson json)
        {
            _json = json;
            _loggerFactory = new NullLoggerFactory();
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

        public ClientBuilder WithLogger(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            return this;
        }

        public ClientBuilder WithStdioTransport(string command, string[] args)
        {
            _transportFactory = new StdioTransportFactory(_json, command, args);
            return this;
        }

        public ClientBuilder WithSseTransport(ISseClientFactory sseClientFactory)
        {
            _transportFactory = new SseTransportFactory(_json, sseClientFactory);
            return this;
        }
        
        public ClientBuilder WithCustomTransport(ITransportFactory transportFactory)
        {
            _transportFactory = transportFactory;
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
        
        public IClient Build()
        {
            if (_name == null)
                throw new ArgumentNullException(nameof(_name), "Client name cannot be null.");
            
            if (_version == null)
                throw new ArgumentNullException(nameof(_version), "Client version cannot be null.");
            
            var clientInfo = new ClientInfo(_name, _version);
            
            var transport = _transportFactory?.Create(_loggerFactory);
            if (transport == null)
                throw new ArgumentNullException(nameof(transport), "Client transport cannot be null.");

            var rootsCapability = _rootsCapabilityFactory?.Create();
            var samplingCapability = _samplingCapabilityFactory?.Create();

            var loggerFactory = _loggerFactory;
            
            return new McpClient(transport, loggerFactory, clientInfo, rootsCapability, samplingCapability);
        }
    }
}