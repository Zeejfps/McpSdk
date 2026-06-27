namespace McpSdk.Protocol.Models;

/// <summary>
/// Params for <c>resources/list</c>. Carries the optional opaque pagination <c>cursor</c>
/// (2025-11-25); omitted on the first page.
/// </summary>
public sealed class ListResourcesRequest : IJsonObjectWriter
{
    public string Cursor { get; }

    public ListResourcesRequest(string cursor = null)
    {
        Cursor = cursor;
    }

    public ListResourcesRequest(IJsonObject jsonObject)
    {
        Cursor = jsonObject?["cursor"]?.AsString();
    }

    public void WriteMembers(IJsonWriter writer)
    {
        Cursor?.WriteTo(writer, "cursor");
    }
}
