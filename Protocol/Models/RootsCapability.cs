namespace McpSdk.Protocol.Models
{
    public sealed class RootsCapability : JsonObjectWrapper
    {
        private bool IsListChangedNotificationSupported { get; }

        public RootsCapability(IJsonObject jsonObject)
        {
            JsonObject = jsonObject;
            IsListChangedNotificationSupported = jsonObject["listChanged"]?.AsBool() ?? false;
        }

        public override IJsonObject JsonObject { get; }
    }
}