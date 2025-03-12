namespace McpSdk.Protocol
{
    public sealed class NumberSchemaWriter
    {
        private readonly IJsonWriter _writer;

        public NumberSchemaWriter(IJsonWriter writer)
        {
            _writer = writer;
            _writer.Write("type", "number");
        }

        public NumberSchemaWriter Min(int minValue)
        {
            _writer.Write("minimum", minValue);
            return this;
        }

        public NumberSchemaWriter Max(int maxValue)
        {
            _writer.Write("maximum", maxValue);
            return this;
        }

        public NumberSchemaWriter Describe(string description)
        {
            _writer.Write("description", description);
            return this;
        }
    }
}