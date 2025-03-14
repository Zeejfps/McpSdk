namespace McpSdk.Protocol.Models.ServerCapabilities
{
    public sealed class ServerCapabilitiesModel
    {
        public ToolsCapabilityModel Tools { get; set; }
        public PromptsCapabilityModel Prompts { get; set; }
        public ResourcesCapabilityModel Resources { get; set; }

        public ServerCapabilitiesModel() {}
        
        public ServerCapabilitiesModel(IJsonObject jsonObject)
        {
            var toolsObj = jsonObject["tools"]?.AsObject();
            if (toolsObj != null)
                Tools = new ToolsCapabilityModel(toolsObj);
            
            var promptsObj = jsonObject["prompts"]?.AsObject();
            if (promptsObj != null)
                Prompts = new PromptsCapabilityModel(promptsObj);
            
            var resourcesObj = jsonObject["resources"]?.AsObject();
            if (resourcesObj != null)
                Resources = new ResourcesCapabilityModel(resourcesObj);
        }
        
        public void AsJson(IJsonWriter writer)
        {
            if (Tools != null)
                writer.Write("tools", Tools.AsJson);
            
            if (Prompts != null)
                writer.Write("prompts", Prompts.AsJson);
            
            if (Resources != null)
                writer.Write("resources", Resources.AsJson);
        }
    }
}