using System;

namespace McpSdk.Protocol
{
    public sealed class SchemaWriter
    {
        private readonly IJsonWriter _writer;

        public SchemaWriter(IJsonWriter writer)
        {
            _writer = writer;
        }

        public SchemaWriter Object(Action<ObjectSchemaWriter> writeObject)
        {
            var objectSchemaWriter = new ObjectSchemaWriter(_writer);
            writeObject(objectSchemaWriter);
            return this;
        }

        public NumberSchemaWriter Number()
        {
            return new NumberSchemaWriter(_writer);
        }

        public StringInputSchemaWriter String()
        {
            return new StringInputSchemaWriter(_writer);
        }

        public BooleanSchemaWriter Boolean()
        {
            return new BooleanSchemaWriter(_writer);
        }

        public ArraySchemaWriter Array()
        {
            return new ArraySchemaWriter(_writer);
        }
    }
}