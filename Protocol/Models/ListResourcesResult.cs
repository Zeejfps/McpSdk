namespace McpSdk.Protocol.Models;

public sealed class ListResourcesResult
{
    public Resource[] Resources { get; set; }
    
    public void AsJson(IJsonWriter jsonwriter)
    {
        
    }
}

public sealed class Resource
{
    
}