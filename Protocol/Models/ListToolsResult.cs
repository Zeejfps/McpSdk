namespace McpSdk.Protocol.Models
{
    public sealed class ListToolsResult : IJsonObjectWriter
    {
        public Tool[] Tools { get; }

        /// <summary>Opaque cursor for the next page (2025-11-25), or null when this is the last page.</summary>
        public string NextCursor { get; }

        public ListToolsResult(Tool[] tools, string nextCursor = null)
        {
            Tools = tools;
            NextCursor = nextCursor;
        }

        public ListToolsResult(IJsonObject jsonObject)
        {
            Tools = jsonObject["tools"].AsArray(t => new Tool(t)) ?? System.Array.Empty<Tool>();
            NextCursor = jsonObject["nextCursor"]?.AsString();
        }

        public void WriteMembers(IJsonWriter writer)
        {
            Tools.WriteTo(writer, "tools");
            NextCursor?.WriteTo(writer, "nextCursor");
        }
    }
}
