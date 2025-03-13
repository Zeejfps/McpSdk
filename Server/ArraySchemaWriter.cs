using McpSdk.Protocol;

namespace McpSdk.Server
{
    public sealed class ArraySchemaWriter
    {
        private readonly IJsonWriter _writer;

        public ArraySchemaWriter(IJsonWriter writer)
        {
            _writer = writer;
        }

        public ArraySchemaWriter MinItems(int min)
        {
            _writer.Write("minItems", min);
            return this;
        }

        public ArraySchemaWriter MaxItems(int max)
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

        public ArraySchemaWriter Describe(string description)
        {
            _writer.Write("description", description);
            return this;
        }

        private void WriteType(string type)
        {
            _writer.Write("items", props =>
            {
                _writer.Write("type", type);
            });
        }
    }
}