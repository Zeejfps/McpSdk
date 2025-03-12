namespace McpSdk.Protocol
{
    public sealed class StringInputSchemaWriter
    {
        private readonly IJsonWriter _writer;

        public StringInputSchemaWriter(IJsonWriter writer)
        {
            _writer = writer;
            _writer.Write("type", "string");
        }

        public StringInputSchemaWriter Min(int min)
        {
            _writer.Write("minLength", min);
            return this;
        }

        public StringInputSchemaWriter Max(int max)
        {
            _writer.Write("maxLength", max);
            return this;
        }

        public StringInputSchemaWriter Describe(string description)
        {
            _writer.Write("description", description);
            return this;
        }
    }
}