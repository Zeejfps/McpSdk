namespace McpSdk.Protocol.Models
{
    public sealed class Tool : IJsonObjectWriter
    {
        // MCP requires every tool to carry an object-root inputSchema. When a tool declares none, emit an
        // empty object schema ({"type":"object","properties":{},...}) rather than omitting the field —
        // strict clients reject the whole tools/list response if inputSchema is missing. An empty schema
        // accepts any arguments, matching the "no validation" behaviour for a tool without a schema.
        private static readonly ObjectSchema EmptyInputSchema = new ObjectSchema();

        public string Name { get; set; }

        /// <summary>Human-friendly display title (2025-06-18); falls back to Name when absent.</summary>
        public string Title { get; set; }

        public string Description { get; set; }
        public ObjectSchema InputSchema { get; set; }

        /// <summary>Optional schema for the tool's structured output (2025-06-18).</summary>
        public ObjectSchema OutputSchema { get; set; }

        /// <summary>Optional behavioural hints (2025-03-26).</summary>
        public ToolAnnotations Annotations { get; set; }

        /// <summary>Optional display icons (2025-11-25).</summary>
        public Icon[] Icons { get; set; }

        /// <summary>Opaque, implementation-defined metadata.</summary>
        public Meta Meta { get; set; }

        public Tool() {}

        public Tool(string name, string description, ObjectSchema schema)
        {
            Name = name;
            Description = description;
            InputSchema = schema;
        }

        public Tool(IJsonObject jsonObj)
        {
            Name = jsonObj["name"].AsString();
            Title = jsonObj["title"]?.AsString();
            Description = jsonObj["description"]?.AsString();

            var inputSchemaObj = jsonObj["inputSchema"]?.AsObject();
            if (inputSchemaObj != null)
                InputSchema = new ObjectSchema(inputSchemaObj);

            var outputSchemaObj = jsonObj["outputSchema"]?.AsObject();
            if (outputSchemaObj != null)
                OutputSchema = new ObjectSchema(outputSchemaObj);

            var annotationsObj = jsonObj["annotations"]?.AsObject();
            if (annotationsObj != null)
                Annotations = new ToolAnnotations(annotationsObj);

            Icons = jsonObj["icons"].AsArray(o => new Icon(o));

            var metaObj = jsonObj["_meta"]?.AsObject();
            if (metaObj != null)
                Meta = new Meta(metaObj);
        }

        public void WriteMembers(IJsonWriter writer)
        {
            writer.Write("name", Name);
            Title?.WriteTo(writer, "title");
            Description?.WriteTo(writer, "description");
            (InputSchema ?? EmptyInputSchema).WriteTo(writer, "inputSchema");
            OutputSchema?.WriteTo(writer, "outputSchema");
            Annotations?.WriteTo(writer, "annotations");
            if (Icons is { Length: > 0 })
                Icons.WriteTo(writer, "icons");
            Meta?.WriteTo(writer, "_meta");
        }
    }
}
