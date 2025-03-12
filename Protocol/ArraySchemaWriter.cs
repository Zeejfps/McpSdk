namespace McpSdk.Protocol
{
    public sealed class ArraySchemaWriter
    {
        private readonly IJsonWriter _writer;

        public ArraySchemaWriter(IJsonWriter writer)
        {
            _writer = writer;
            _writer.Write("type", "array");
        }

        public ArraySchemaWriter Min(int min)
        {
            _writer.Write("minItems", min);
            return this;
        }

        public ArraySchemaWriter Max(int max)
        {
            _writer.Write("maxItems", max);
            return this;
        }

        public ArraySchemaWriter Number()
        {
            WriteType("number");
            return this;
        }

        public ArraySchemaWriter String()
        {
            WriteType("number");
            return this;
        }

        public ArraySchemaWriter Boolean()
        {
            WriteType("boolean");
            return this;
        }

        private void WriteType(string type)
        {
            _writer.Write("items", props =>
            {
                _writer.Write("type", "number");
            });
        }
    }
}