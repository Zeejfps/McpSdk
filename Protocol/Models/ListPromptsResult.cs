namespace McpSdk.Protocol.Models;

public sealed class ListPromptsResult : IJsonObjectWriter
{
    /// <summary>Opaque cursor for the next page (2025-11-25), or null when this is the last page.</summary>
    public string NextCursor { get; }

    public ListPromptsResult(string nextCursor = null)
    {
        NextCursor = nextCursor;
    }

    public ListPromptsResult(IJsonObject jsonObject)
    {
        NextCursor = jsonObject?["nextCursor"]?.AsString();
    }

    public void WriteMembers(IJsonWriter jsonWriter)
    {
        NextCursor?.WriteTo(jsonWriter, "nextCursor");
    }
}
