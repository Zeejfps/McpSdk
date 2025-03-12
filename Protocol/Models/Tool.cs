using System;

namespace McpSdk.Protocol.Models
{
    public sealed class Tool : JsonObjectWrapper
    {
        public Tool(IJson json, string name, string description, IJsonObject inputSchema)
        {
            Name = name;
            Description = description;
            InputSchema = inputSchema;
            JsonObject = json.Build(props =>
            {
                props.Write("name", Name);
                props.Write("description", Description);
                props.Write("inputSchema", InputSchema);
            });
        }
        
        public Tool(IJsonObject jsonObj)
        {
            JsonObject = jsonObj;
            Name = jsonObj["name"].AsString();
            Description = jsonObj["description"].AsString();
            InputSchema = jsonObj["inputSchema"].AsObject();
        }

        public override IJsonObject JsonObject { get; }
        public string Name { get; }
        public string Description { get; }
        public IJsonObject InputSchema { get; }

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

            public Writer WriteInputSchema(Action<ObjectSchemaWriter> writeInputSchema)
            {
                _writer.Write("inputSchema", props =>
                {
                    var objectSchemaWriter = new ObjectSchemaWriter(_writer);
                    writeInputSchema(objectSchemaWriter);
                });
                return this;
            }
        }
    }
}