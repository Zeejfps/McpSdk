namespace McpSdk.Protocol.Models;

/// <summary>
/// Params for <c>resources/templates/list</c>. Carries the optional opaque pagination
/// <c>cursor</c> (2025-11-25); omitted on the first page.
/// </summary>
public sealed class ListTemplatesRequest : IJsonObjectWriter
{
    public string Cursor { get; }

    public ListTemplatesRequest(string cursor = null)
    {
        Cursor = cursor;
    }

    public ListTemplatesRequest(IJsonObject jsonObject)
    {
        Cursor = jsonObject?["cursor"]?.AsString();
    }

    public void WriteMembers(IJsonWriter writer)
    {
        Cursor?.WriteTo(writer, "cursor");
    }
}
