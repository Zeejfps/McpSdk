namespace McpSdk.Protocol.Models.ServerCapabilities;

public sealed class ResourcesCapabilityModel : IJsonObjectWriter
{
    public bool? IsResourceChangedNotificationSupported { get; set; }
    public bool? IsListChangedNotificationSupported { get; set; }

    public ResourcesCapabilityModel()
    {
    }
        
    public ResourcesCapabilityModel(IJsonObject jsonObject)
    {
        IsListChangedNotificationSupported = jsonObject["listChanged"]?.AsBool() ?? false;   
    }

    public void WriteMembers(IJsonWriter writer)
    {
        if (IsResourceChangedNotificationSupported.HasValue)
            writer.Write("subscribe", IsResourceChangedNotificationSupported.Value);
        
        if (IsListChangedNotificationSupported.HasValue) 
            writer.Write("listChanged", IsListChangedNotificationSupported.Value);
    }
}