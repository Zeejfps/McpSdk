using System;

namespace McpSdk.Protocol
{
    public sealed class ObjectSchemaWriter
    {
        private readonly IJsonWriter _writer;

        public ObjectSchemaWriter(IJsonWriter writer)
        {
            _writer = writer;
        }

        public ObjectSchemaWriter Prop(string name, Action<SchemaWriter> writeProp)
        {
            _writer.Write(name, propWriter =>
            {
                var schemaWriter = new SchemaWriter(propWriter);
                writeProp(schemaWriter);
            });
            return this;
        }
    }
}