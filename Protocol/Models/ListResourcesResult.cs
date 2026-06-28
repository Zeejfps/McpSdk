namespace McpSdk.Protocol.Models;

public sealed class ListResourcesResult : IJsonObjectWriter
{
    public Resource[] Resources { get; }

    /// <summary>Opaque cursor for the next page (2025-11-25), or null when this is the last page.</summary>
    public string NextCursor { get; }

    public ListResourcesResult(Resource[] resources, string nextCursor = null)
    {
        Resources = resources;
        NextCursor = nextCursor;
    }

    public ListResourcesResult(IJsonObject jsonObject)
    {
        Resources = jsonObject["resources"].AsArray(r => new Resource(r)) ?? System.Array.Empty<Resource>();
        NextCursor = jsonObject["nextCursor"]?.AsString();
    }

    public void WriteMembers(IJsonWriter jsonWriter)
    {
        Resources.WriteTo(jsonWriter, "resources");
        NextCursor?.WriteTo(jsonWriter, "nextCursor");
    }
}
