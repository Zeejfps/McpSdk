namespace McpSdk.Protocol.Models.ServerCapabilities
{
    public sealed class ServerCapabilitiesModel : IJsonObjectWriter
    {
        public ToolsCapabilityModel Tools { get; set; }
        public PromptsCapabilityModel Prompts { get; set; }
        public ResourcesCapabilityModel Resources { get; set; }
        public LoggingCapabilityModel Logging { get; set; }
        public CompletionCapabilityModel Completion { get; set; }

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
            
            var loggingObj = jsonObject["logging"]?.AsObject();
            if (loggingObj != null)
                Logging = new LoggingCapabilityModel(loggingObj);
            
            var completionObj = jsonObject["completion"]?.AsObject();
            if (completionObj != null)
                Completion = new CompletionCapabilityModel(completionObj);
        }
        
        public void WriteMembers(IJsonWriter writer)
        {
            if (Tools != null)
                writer.Write("tools", Tools);
            
            if (Prompts != null)
                writer.Write("prompts", Prompts);
            
            if (Resources != null)
                writer.Write("resources", Resources);
            
            if (Logging != null)
                writer.Write("logging", Logging);
            
            if (Completion != null)
                writer.Write("completion", Completion);
        }
    }
}