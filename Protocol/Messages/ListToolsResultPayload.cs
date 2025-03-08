namespace McpSharp.Protocol.Messages
{
    public sealed class ListToolsResultPayload
    {
        public Tool[] Tools { get; }

        public ListToolsResultPayload(Tool[] tools)
        {
            Tools = tools;
        }
    }
}