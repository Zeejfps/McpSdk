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

        public SchemaWriter Object(string name, Action<SchemaWriter> writeObjSchema)
        {
            _writer.Write(name, objWriter =>
            {
                _writer.Write("type", "object");
                _writer.Write("properties", propertyWriter =>
                {
                    var objSchemaWriter = new SchemaWriter(propertyWriter);
                    writeObjSchema(objSchemaWriter);
                });
            });
           
            return this;
        }

        public SchemaWriter Number(string name, Action<NumberSchemaWriter> writeNumberSchema)
        {
            _writer.Write(name, writer =>
            {
                writer.Write("type", "number");
                var numberSchemaWriter = new NumberSchemaWriter(_writer);
                writeNumberSchema(numberSchemaWriter);
            });
            return this;
        }

        public SchemaWriter String(string name, Action<StringSchemaWriter> writeStringSchema)
        {
            _writer.Write(name, writer =>
            {
                writer.Write("type", "string");
                var schemaWriter = new StringSchemaWriter(_writer);
                writeStringSchema(schemaWriter);
            });
            return this;
        }

        public SchemaWriter Boolean(string name, Action<BooleanSchemaWriter> writeStringSchema)
        {
            _writer.Write(name, writer =>
            {
                writer.Write("type", "boolean");
                var schemaWriter = new BooleanSchemaWriter(_writer);
                writeStringSchema(schemaWriter);
            });
            return this;
        }

        public SchemaWriter Array(string name, Action<ArraySchemaWriter> writeSchema)
        {
            _writer.Write(name, writer =>
            {
                writer.Write("type", "array");
                var schemaWriter = new ArraySchemaWriter(_writer);
                writeSchema(schemaWriter);
            });
            return this;
        }
    }
}