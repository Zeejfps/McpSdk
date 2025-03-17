namespace McpSdk.Protocol.Models;

public sealed class EmbeddedResourceContent : Content
{ 
    public ResourceContent Resource { get; }
        
    public EmbeddedResourceContent(IJsonObject jsonObject)
    {
        var resourceObj = jsonObject["resource"].AsObject();
        Resource = new ResourceContent(resourceObj);
    }

    public override void AsJson(IJsonWriter writer)
    {
        writer.Write("type", "resource");
        writer.Write("resource", Resource.AsJson);
    }
}