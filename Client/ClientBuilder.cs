using System;
using McpSharp.Protocol;

namespace McpSharp.Client
{
    public sealed class ClientBuilder
    {
        private readonly IJson _json;
        
        private string _name;
        private string _version;
        private ITransportFactory _transportFactory;
        private IRootsCapabilityFactory _rootsCapabilityFactory;
        private ISamplingCapabilityFactory _samplingCapabilityFactory;

        public ClientBuilder(IJson json)
        {
            _json = json;
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

        public ClientBuilder WithSseTransport(ISseClientFactory sseClientFactory, string host)
        {
            _transportFactory = new SseTransportFactory(_json, sseClientFactory, host);
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
            
            var transport = _transportFactory?.Create();
            if (transport == null)
                throw new ArgumentNullException(nameof(transport), "Client transport cannot be null.");

            var rootsCapability = _rootsCapabilityFactory?.Create();

            var samplingCapabilityController = new SamplingCapabilityController(_json);
            var samplingCapability = _samplingCapabilityFactory?.Create(samplingCapabilityController);
            
            return new McpClient(transport, clientInfo, rootsCapability, samplingCapability);
        }
    }

    internal sealed class SamplingCapabilityController : ISamplingCapabilityController
    {
        private readonly IJson _json;

        public SamplingCapabilityController(IJson json)
        {
            _json = json;
        }

        public Content CreateTextContent(string text)
        {
            var jsonText = _json.Stringify(props =>
            {
                props.Write("type", "text");
                props.Write("text", text);
            });
            return new TextContent(_json.Parse(jsonText));
        }

        public Content CreateImageContent(byte[] imageBytes)
        {
            throw new NotImplementedException();
        }

        public CreateMessagesResult CreateResult(string role, string model, string stopReason, Content content)
        {
            throw new NotImplementedException();
        }
    }
}