namespace McpSdk.Protocol.Models.ServerCapabilities
{
    public sealed class ToolsCapabilityModel
    {
        public bool IsListChangedNotificationSupported { get; }

        public ToolsCapabilityModel(bool isListChangedNotificationSupported)
        {
            IsListChangedNotificationSupported = isListChangedNotificationSupported;
        }
        
        public ToolsCapabilityModel(IJsonObject jsonObject)
        {
            IsListChangedNotificationSupported = jsonObject["listChanged"]?.AsBool() ?? false;   
        }

        public void AsJson(IJsonWriter writer)
        {
            writer.Write("listChanged", IsListChangedNotificationSupported);
        }
    }
}