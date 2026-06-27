namespace McpSdk.Protocol.Models;

public sealed class EmbeddedResourceContent : Content
{ 
    public ResourceContents Resource { get; }
        
    public EmbeddedResourceContent(IJsonObject jsonObject)
    {
        var resourceObj = jsonObject["resource"].AsObject();
        Resource = ResourceContents.FromJsonObject(resourceObj);
    }

    public override void WriteMembers(IJsonWriter writer)
    {
        writer.Write("type", "resource");
        writer.Write("resource", Resource);
    }
}