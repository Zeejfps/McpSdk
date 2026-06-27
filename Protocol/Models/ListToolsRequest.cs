namespace McpSdk.Protocol.Models
{
    /// <summary>
    /// Params for <c>tools/list</c>. Carries the optional opaque pagination <c>cursor</c>
    /// (2025-11-25); omitted on the first page.
    /// </summary>
    public sealed class ListToolsRequest : IJsonObjectWriter
    {
        public string Cursor { get; }

        public ListToolsRequest(string cursor = null)
        {
            Cursor = cursor;
        }

        public ListToolsRequest(IJsonObject jsonObject)
        {
            Cursor = jsonObject?["cursor"]?.AsString();
        }

        public void WriteMembers(IJsonWriter writer)
        {
            if (Cursor != null)
                writer.Write("cursor", Cursor);
        }
    }
}
