namespace McpSdk.Protocol
{
    public sealed class ToolBuilder
    {
        private readonly IJson _json;
        
        public ToolBuilder(IJson json)
        {
            _json = json;
        }

        public ToolBuilder Name(string name)
        {
            return this;
        }

        public ToolBuilder Description(string description)
        {
            return this;
        }

        public Tool Build()
        {
            return null;
        }
    }
}