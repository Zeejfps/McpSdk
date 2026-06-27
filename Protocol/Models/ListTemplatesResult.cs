namespace McpSdk.Protocol.Models;

public sealed class ListTemplatesResult : IJsonObjectWriter
{
    /// <summary>Opaque cursor for the next page (2025-11-25), or null when this is the last page.</summary>
    public string NextCursor { get; }

    public ListTemplatesResult(string nextCursor = null)
    {
        NextCursor = nextCursor;
    }

    public ListTemplatesResult(IJsonObject jsonObject)
    {
        NextCursor = jsonObject?["nextCursor"]?.AsString();
    }

    public void WriteMembers(IJsonWriter writer)
    {
        if (NextCursor != null)
            writer.Write("nextCursor", NextCursor);
    }
}
