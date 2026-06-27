using McpSdk.Protocol.Models.ServerCapabilities;

namespace McpSdk.Protocol.Models
{
    public sealed class InitializeResult : IJsonSerializable
    {
        public string ProtocolVersion { get; }
        public ServerCapabilitiesModel Capabilities { get; }

        public ServerInfo ServerInfo { get; }

        public Meta Meta { get; }

        public InitializeResult(string protocolVersion, ServerCapabilitiesModel capabilities, ServerInfo serverInfo, Meta meta = null)
        {
            ProtocolVersion = protocolVersion;
            Capabilities = capabilities;
            ServerInfo = serverInfo;
            Meta = meta;
        }

        public InitializeResult(IJsonObject jsonObject)
        {
            ProtocolVersion = jsonObject["protocolVersion"]?.AsString();

            var capabilities = jsonObject["capabilities"]?.AsObject();
            if (capabilities != null)
                Capabilities = new ServerCapabilitiesModel(capabilities);

            var serverInfo = jsonObject["serverInfo"]?.AsObject();
            if (serverInfo != null)
                ServerInfo = new ServerInfo(serverInfo);

            var metaObj = jsonObject["_meta"]?.AsObject();
            if (metaObj != null)
                Meta = new Meta(metaObj);
        }

        public void AsJson(IJsonWriter writer)
        {
            writer.Write("protocolVersion", ProtocolVersion);

            if (Capabilities != null)
                writer.Write("capabilities", Capabilities);

            if (ServerInfo != null)
                writer.Write("serverInfo", ServerInfo);

            if (Meta != null)
                writer.Write("_meta", Meta);
        }
    }
}
