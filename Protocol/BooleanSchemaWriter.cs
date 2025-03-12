namespace McpSdk.Protocol
{
    public sealed class BooleanSchemaWriter
    {
        private readonly IJsonWriter _writer;

        public BooleanSchemaWriter(IJsonWriter writer)
        {
            _writer = writer;
            _writer.Write("type", "boolean");
        }

        public BooleanSchemaWriter Describe(string description)
        {
            _writer.Write("description", description);
            return this;
        }
    }
}