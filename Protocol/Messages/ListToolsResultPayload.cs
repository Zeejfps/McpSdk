namespace McpSharp.Protocol.Messages
{
    public sealed class ListToolsResultPayload
    {
        public ToolInfo[] Tools { get; }

        public ListToolsResultPayload(ToolInfo[] tools)
        {
            Tools = tools;
        }
    }

    public sealed class ToolInfo
    {
        public string Name { get; }
        public string Description { get; }
    }
}