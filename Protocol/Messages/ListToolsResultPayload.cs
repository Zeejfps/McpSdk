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
}