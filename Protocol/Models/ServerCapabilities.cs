namespace McpSdk.Protocol.Models
{
    public sealed class ServerCapabilities
    {
        public ToolsCapability Tools { get; set; }

        public ServerCapabilities() {}
        
        public ServerCapabilities(IJsonObject jsonObject)
        {
            var toolsObj = jsonObject["tools"]?.AsObject();
            if (toolsObj != null)
                Tools = new ToolsCapability(toolsObj);
        }
        
        public void AsJson(IJsonWriter writer)
        {
            if (Tools != null)
                writer.Write("tools", Tools.AsJson);
        }
    }
}