namespace McpSdk.Protocol.Models.ServerCapabilities;

public sealed class PromptsCapabilityModel : IJsonObjectWriter
{
    public bool IsListChangedNotificationSupported { get; }

    public PromptsCapabilityModel(bool isListChangedNotificationSupported)
    {
        IsListChangedNotificationSupported = isListChangedNotificationSupported;
    }

    public PromptsCapabilityModel(IJsonObject jsonObject)
    {
        IsListChangedNotificationSupported = jsonObject["listChanged"]?.AsBool() ?? false;
    }

    public void WriteMembers(IJsonWriter writer)
    {
        writer.Write("listChanged", IsListChangedNotificationSupported);
    }
}