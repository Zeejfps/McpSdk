namespace McpSdk.Protocol.Models;

public sealed class PromptsCapability
{
    public bool IsListChangedNotificationSupported { get; }

    public PromptsCapability(bool isListChangedNotificationSupported)
    {
        IsListChangedNotificationSupported = isListChangedNotificationSupported;
    }

    public PromptsCapability(IJsonObject jsonObject)
    {
        IsListChangedNotificationSupported = jsonObject["listChanged"]?.AsBool() ?? false;
    }

    public void AsJson(IJsonWriter writer)
    {
        writer.Write("listChanged", IsListChangedNotificationSupported);
    }
}