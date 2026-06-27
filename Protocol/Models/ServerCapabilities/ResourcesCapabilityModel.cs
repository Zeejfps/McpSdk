namespace McpSdk.Protocol.Models.ServerCapabilities;

public sealed class ResourcesCapabilityModel : IJsonSerializable
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

    public void AsJson(IJsonWriter writer)
    {
        if (IsResourceChangedNotificationSupported.HasValue)
            writer.Write("subscribe", IsResourceChangedNotificationSupported.Value);
        
        if (IsListChangedNotificationSupported.HasValue) 
            writer.Write("listChanged", IsListChangedNotificationSupported.Value);
    }
}