using System;
using McpSdk.Protocol;

namespace McpSdk.Server;

public sealed class ToolWriter
{
    private readonly IJsonWriter _writer;
            
    public ToolWriter(IJsonWriter writer)
    {
        _writer = writer;
    }

    public ToolWriter WriteName(string name)
    {
        _writer.Write("name", name);
        return this;
    }

    public ToolWriter WriteDescription(string description)
    {
        _writer.Write("description", description);
        return this;
    }

    public ToolWriter WriteInputSchema(Action<SchemaWriter> writeInputSchema)
    {
        var schemaWriter = new SchemaWriter(_writer);
        schemaWriter.Object("inputSchema", writeInputSchema);
        return this;
    }
}