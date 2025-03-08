using System.Collections.Generic;

namespace McpSharp.Protocol.Messages
{
    public sealed class CallToolRequestPayload
    {
        public CallToolRequestPayload(string toolName, Dictionary<string, object> arguments)
        {
            ToolName = toolName;
            Arguments = arguments;
        }

        public string ToolName { get; }

        public Dictionary<string, object> Arguments { get; }
    }
}