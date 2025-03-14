namespace McpSdk.Protocol.Models.ServerCapabilities
{
    public sealed class ServerCapabilitiesModel
    {
        public ToolsCapabilityModel Tools { get; set; }
        public PromptsCapabilityModel Prompts { get; set; }

        public ServerCapabilitiesModel() {}
        
        public ServerCapabilitiesModel(IJsonObject jsonObject)
        {
            var toolsObj = jsonObject["tools"]?.AsObject();
            if (toolsObj != null)
                Tools = new ToolsCapabilityModel(toolsObj);
        }
        
        public void AsJson(IJsonWriter writer)
        {
            if (Tools != null)
                writer.Write("tools", Tools.AsJson);
        }
    }
}