using McpSdk.Protocol.Models.ServerCapabilities;

namespace McpSdk.Protocol.Models
{
    public sealed class InitializeResult : IJsonObjectWriter
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

        public void WriteMembers(IJsonWriter writer)
        {
            writer.Write("protocolVersion", ProtocolVersion);
            Capabilities?.WriteTo(writer, "capabilities");
            ServerInfo?.WriteTo(writer, "serverInfo");
            Meta?.WriteTo(writer, "_meta");
        }
    }
}
