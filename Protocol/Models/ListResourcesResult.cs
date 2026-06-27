namespace McpSdk.Protocol.Models;

public sealed class ListResourcesResult : IJsonSerializable
{
    public Resource[] Resources { get; }

    public ListResourcesResult(Resource[] resources)
    {
        Resources = resources;
    }

    public ListResourcesResult(IJsonObject jsonObject)
    {
        var resources = jsonObject["resources"].AsObjectArray();
        Resources = new Resource[resources.Length];
        for (var i = 0; i < resources.Length; i++)
        {
            Resources[i] = new Resource(resources[i]);
        }
    }
    
    public void AsJson(IJsonWriter jsonWriter)
    {
        jsonWriter.Write("resources", Resources);
    }
}