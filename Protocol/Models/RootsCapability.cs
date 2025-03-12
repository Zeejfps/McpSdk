namespace McpSdk.Protocol.Models
{
    public sealed class RootsCapability
    {
        private bool IsListChangedNotificationSupported { get; }

        public RootsCapability(bool isListChangedNotificationSupported)
        {
            IsListChangedNotificationSupported = isListChangedNotificationSupported;
        }

        public RootsCapability(IJsonObject jsonObject)
        {
            IsListChangedNotificationSupported = jsonObject["listChanged"]?.AsBool() ?? false;
        }

        public void ToJson(IJsonWriter writer)
        {
            writer.Write("listChanged", IsListChangedNotificationSupported);
        }
    }
}