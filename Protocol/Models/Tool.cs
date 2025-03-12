using System;

namespace McpSdk.Protocol.Models
{
    public sealed class Tool
    {
        public string Name { get; }
        public string Description { get; }
        public IJsonObject InputSchema { get; }

        public Tool(string name, string description, IJsonObject schema)
        {
            Name = name;
            Description = description;
            InputSchema = schema;
        }
        
        public Tool(IJsonObject jsonObj)
        {
            Name = jsonObj["name"].AsString();
            Description = jsonObj["description"].AsString();
            InputSchema = jsonObj["inputSchema"].AsObject();
        }

        public void ToJson(IJsonWriter writer)
        {
            writer.Write("name", Name);
            writer.Write("description", Description);
            writer.Write("inputSchema", InputSchema);
        }

        public static Writer CreateWriter(IJsonWriter jsonWriter)
        {
            return new Writer(jsonWriter);
        }

        public sealed class Writer
        {
            private readonly IJsonWriter _writer;
            
            public Writer(IJsonWriter writer)
            {
                _writer = writer;
            }

            public Writer WriteName(string name)
            {
                _writer.Write("name", name);
                return this;
            }

            public Writer WriteDescription(string description)
            {
                _writer.Write("description", description);
                return this;
            }

            public Writer WriteInputSchema(Action<SchemaWriter> writeInputSchema)
            {
                var schemaWriter = new SchemaWriter(_writer);
                schemaWriter.Object("inputSchema", writeInputSchema);
                return this;
            }
        }
    }
}