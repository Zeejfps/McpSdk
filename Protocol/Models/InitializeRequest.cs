using System;

namespace McpSdk.Protocol.Models
{
    public sealed class InitializeRequest : JsonObjectWrapper
    {
        public InitializeRequest(IJsonObject jsonObject)
        {
            JsonObject = jsonObject;
            ProtocolVersion = jsonObject["protocolVersion"].AsString();
            
            var capabilities = jsonObject["capabilities"].AsObject();
            ClientCapabilities = new ClientCapabilities(capabilities);
            
            var clientInfo = jsonObject["clientInfo"].AsObject();
            var clientName = clientInfo["name"].AsString();
            var clientVersion = clientInfo["version"].AsString();
            ClientInfo = new ClientInfo(clientName, clientVersion);
        }
        
        public string ProtocolVersion { get; }
        public ClientInfo ClientInfo { get; }
        public ClientCapabilities ClientCapabilities { get; }
        public override IJsonObject JsonObject { get; }

        public static Writer CreateWriter(IJsonWriter writer)
        {
            return new Writer(writer);
        }
        
        public sealed class Writer
        {
            private readonly IJsonWriter _writer;

            public Writer(IJsonWriter writer)
            {
                _writer = writer;
            }

            public Writer WriteProtocolVersion(string protocolVersion)
            {
                _writer.Write("protocolVersion", protocolVersion);
                return this;
            }

            public Writer WriteClientInfo(string name, string version)
            {
                _writer.Write("clientInfo", clientInfo =>
                {
                    _writer.Write("name", name);
                    _writer.Write("version", version);
                });
                return this;
            }

            public Writer WriteCapabilities(Action<ClientCapabilities.Writer> writeCapabilities)
            {
                _writer.Write("capabilities", props =>
                {
                    var capabilitiesWriter = ClientCapabilities.CreateWriter(props);
                    writeCapabilities(capabilitiesWriter);
                });
                return this;
            }
        }
    }
}