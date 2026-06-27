using McpSdk.Protocol.Models.ClientCapabilities;

namespace McpSdk.Protocol.Models
{
    public sealed class InitializeRequest : IJsonObjectWriter
    {
        public string ProtocolVersion { get; }
        public ClientInfo ClientInfo { get; }
        public ClientCapabilitiesModel ClientCapabilities { get; }
        public Meta Meta { get; }

        public InitializeRequest(string protocolVersion, ClientCapabilitiesModel capabilities, ClientInfo clientInfo, Meta meta = null)
        {
            ProtocolVersion = protocolVersion;
            ClientCapabilities = capabilities;
            ClientInfo = clientInfo;
            Meta = meta;
        }

        public InitializeRequest(IJsonObject jsonObject)
        {
            ProtocolVersion = jsonObject["protocolVersion"].AsString();

            var capabilities = jsonObject["capabilities"].AsObject();
            ClientCapabilities = new ClientCapabilitiesModel(capabilities);

            var clientInfoObj = jsonObject["clientInfo"]?.AsObject();
            if (clientInfoObj != null)
                ClientInfo = new ClientInfo(clientInfoObj);

            var metaObj = jsonObject["_meta"]?.AsObject();
            if (metaObj != null)
                Meta = new Meta(metaObj);
        }

        public void WriteMembers(IJsonWriter writer)
        {
            writer.Write("protocolVersion", ProtocolVersion);
            writer.Write("capabilities", ClientCapabilities);
            writer.Write("clientInfo", ClientInfo);

            Meta?.WriteTo(writer, "_meta");
        }
    }
}