using System;

namespace McpSdk.Protocol.Models
{
    public sealed class InitializeRequest : JsonObjectWrapper
    {
        public InitializeRequest(string protocolVersion, ClientCapabilities capabilities, ClientInfo clientInfo)
        {
            ProtocolVersion = protocolVersion;
            ClientCapabilities = capabilities;
            ClientInfo = clientInfo;
        }
        
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

        public void ToJson(IJsonWriter writer)
        {
            writer.Write("protocolVersion", ProtocolVersion);
            writer.Write("capabilities", ClientCapabilities.ToJson);
            writer.Write("clientInfo", ClientInfo.ToJson);
        }
    }
}