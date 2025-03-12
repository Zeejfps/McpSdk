namespace McpSdk.Protocol.Models
{
    public sealed class ToolsCapability
    {
        public bool IsListChangedNotificationSupported { get; }

        public ToolsCapability(bool isListChangedNotificationSupported)
        {
            IsListChangedNotificationSupported = isListChangedNotificationSupported;
        }
        
        public ToolsCapability(IJsonObject jsonObject)
        {
            IsListChangedNotificationSupported = jsonObject["listChanged"]?.AsBool() ?? false;   
        }

        public void AsJson(IJsonWriter writer)
        {
            writer.Write("listChanged", IsListChangedNotificationSupported);
        }
    }
}