using McpSdk.Protocol;

namespace McpSdk.Server
{
    public sealed class BooleanSchemaWriter
    {
        private readonly IJsonWriter _writer;

        public BooleanSchemaWriter(IJsonWriter writer)
        {
            _writer = writer;
        }

        public BooleanSchemaWriter Describe(string description)
        {
            _writer.Write("description", description);
            return this;
        }
    }
}