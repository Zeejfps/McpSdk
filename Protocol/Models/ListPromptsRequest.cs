namespace McpSdk.Protocol.Models;

/// <summary>
/// Params for <c>prompts/list</c>. Carries the optional opaque pagination <c>cursor</c>
/// (2025-11-25); omitted on the first page.
/// </summary>
public sealed class ListPromptsRequest : IJsonObjectWriter
{
    public string Cursor { get; }

    public ListPromptsRequest(string cursor = null)
    {
        Cursor = cursor;
    }

    public ListPromptsRequest(IJsonObject jsonObject)
    {
        Cursor = jsonObject?["cursor"]?.AsString();
    }

    public void WriteMembers(IJsonWriter writer)
    {
        Cursor?.WriteTo(writer, "cursor");
    }
}
