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
            var toolsArray = jsonObject["tools"].AsObjectArray();
            var toolsCount = toolsArray.Length;
            var tools = new Tool[toolsCount];
            for (var i = 0; i < toolsCount; i++)
            {
                var toolObj = toolsArray[i];
                tools[i] = new Tool(toolObj);
            }
            Tools = tools;
            NextCursor = jsonObject["nextCursor"]?.AsString();
        }

        public void WriteMembers(IJsonWriter writer)
        {
            Tools.WriteTo(writer, "tools");
            if (NextCursor != null)
                writer.Write("nextCursor", NextCursor);
        }
    }
}
