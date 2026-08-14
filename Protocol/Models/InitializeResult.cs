using McpSdk.Protocol.Models.ServerCapabilities;

namespace McpSdk.Protocol.Models
{
    public sealed class InitializeResult : IJsonObjectWriter
    {
        public string ProtocolVersion { get; }
        public ServerCapabilitiesModel Capabilities { get; }

        public ServerInfo ServerInfo { get; }

        /// <summary>
        /// Free-form guidance describing how to use the server as a whole, which a client MAY surface
        /// to the model as a system prompt. Optional; omitted from the wire payload when null.
        /// </summary>
        public string Instructions { get; }

        public Meta Meta { get; }

        public InitializeResult(string protocolVersion, ServerCapabilitiesModel capabilities, ServerInfo serverInfo, string instructions = null, Meta meta = null)
        {
            ProtocolVersion = protocolVersion;
            Capabilities = capabilities;
            ServerInfo = serverInfo;
            Instructions = instructions;
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

            Instructions = jsonObject["instructions"]?.AsString();

            var metaObj = jsonObject["_meta"]?.AsObject();
            if (metaObj != null)
                Meta = new Meta(metaObj);
        }

        public void WriteMembers(IJsonWriter writer)
        {
            writer.Write("protocolVersion", ProtocolVersion);
            Capabilities?.WriteTo(writer, "capabilities");
            ServerInfo?.WriteTo(writer, "serverInfo");
            Instructions?.WriteTo(writer, "instructions");
            Meta?.WriteTo(writer, "_meta");
        }
    }
}
