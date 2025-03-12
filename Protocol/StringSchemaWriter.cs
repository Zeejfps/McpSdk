namespace McpSdk.Protocol
{
    public sealed class StringSchemaWriter
    {
        private readonly IJsonWriter _writer;

        public StringSchemaWriter(IJsonWriter writer)
        {
            _writer = writer;
        }

        public StringSchemaWriter MinLength(int min)
        {
            _writer.Write("minLength", min);
            return this;
        }

        public StringSchemaWriter MaxLength(int max)
        {
            _writer.Write("maxLength", max);
            return this;
        }

        public StringSchemaWriter Describe(string description)
        {
            _writer.Write("description", description);
            return this;
        }
    }
}