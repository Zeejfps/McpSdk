namespace McpSdk.Protocol.Models.ClientCapabilities
{
    public sealed class RootsCapabilityModel
    {
        private bool IsListChangedNotificationSupported { get; }

        public RootsCapabilityModel(bool isListChangedNotificationSupported)
        {
            IsListChangedNotificationSupported = isListChangedNotificationSupported;
        }

        public RootsCapabilityModel(IJsonObject jsonObject)
        {
            IsListChangedNotificationSupported = jsonObject["listChanged"]?.AsBool() ?? false;
        }

        public void AsJson(IJsonWriter writer)
        {
            writer.Write("listChanged", IsListChangedNotificationSupported);
        }
    }
}