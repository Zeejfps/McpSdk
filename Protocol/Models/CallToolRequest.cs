namespace McpSdk.Protocol.Models
{
    public sealed class CallToolRequest : IJsonObjectWriter
    {
        public string ToolName { get; }
        public IJsonObject ToolArguments { get; }
        
        private const string ToolNameProp = "name";
        private const string ArgumentsProp = "arguments";
        
        public CallToolRequest(string toolName, IJsonObject arguments)
        {
            ToolName = toolName;
            ToolArguments = arguments;
        }
        
        public CallToolRequest(IJsonObject jsonObject)
        {
            ToolName = jsonObject[ToolNameProp]?.AsString();
            ToolArguments = jsonObject[ArgumentsProp]?.AsObject();
        }
        
        public void WriteMembers(IJsonWriter writer)
        {
            writer.Write(ToolNameProp, ToolName);
            writer.Write(ArgumentsProp, ToolArguments);
        }
    }
}