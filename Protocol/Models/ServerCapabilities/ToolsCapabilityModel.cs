namespace McpSdk.Protocol.Models.ServerCapabilities
{
    public sealed class ToolsCapabilityModel : IJsonObjectWriter
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

        public void WriteMembers(IJsonWriter writer)
        {
            writer.Write("listChanged", IsListChangedNotificationSupported);
        }
    }
}