namespace McpSdk.Protocol.Models;

public sealed class BooleanSchema : JsonSchema
{
    public const string Type = "boolean";

    /// <summary>Human-friendly display title (elicitation, 2025-11-25).</summary>
    public string Title { get; set; }

    public string Description { get; set; }

    /// <summary>An optional default value clients should pre-populate (elicitation, SEP-1034).</summary>
    public bool? Default { get; set; }

    public BooleanSchema()
    {

    }

    public BooleanSchema(IJsonObject jsonObject)
    {
        Title = jsonObject["title"]?.AsString();
        Description = jsonObject["description"]?.AsString();
        Default = jsonObject["default"]?.AsBool();
    }

    public override void WriteMembers(IJsonWriter writer)
    {
        writer.Write("type", Type);
        Title?.WriteTo(writer, "title");
        Default?.WriteTo(writer, "default");
        Description?.WriteTo(writer, "description");
    }
}
