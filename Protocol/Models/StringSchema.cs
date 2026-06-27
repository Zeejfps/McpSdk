namespace McpSdk.Protocol.Models;

public sealed class StringSchema : JsonSchema
{
    public const string Type = "string";

    /// <summary>Human-friendly display title (elicitation, 2025-11-25).</summary>
    public string Title { get; set; }
    public string Description { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }

    /// <summary>An optional <c>format</c> hint. Elicitation supports <c>email</c>, <c>uri</c>, <c>date</c>, <c>date-time</c>.</summary>
    public string Format { get; set; }
    public string Pattern { get; set; }
    public string[] Options { get; set; }

    /// <summary>An optional default value clients should pre-populate (elicitation, SEP-1034).</summary>
    public string Default { get; set; }

    public StringSchema() {}

    public StringSchema(IJsonObject jsonObject)
    {
        Title = jsonObject["title"]?.AsString();
        MinLength = jsonObject["minLength"]?.AsInt();
        MaxLength = jsonObject["maxLength"]?.AsInt();
        Description = jsonObject["description"]?.AsString();
        Format = jsonObject["format"]?.AsString();
        Pattern = jsonObject["pattern"]?.AsString();
        Options = jsonObject["enum"]?.AsStringArray();
        Default = jsonObject["default"]?.AsString();
    }

    public override void WriteMembers(IJsonWriter writer)
    {
        writer.Write("type", Type);
        if (Title != null)
            writer.Write("title", Title);
        if (MinLength.HasValue)
            writer.Write("minLength", MinLength.Value);
        if (MaxLength.HasValue)
            writer.Write("maxLength", MaxLength.Value);
        if (Format != null)
            writer.Write("format", Format);
        if (Pattern != null)
            writer.Write("pattern", Pattern);
        if (Options != null && Options.Length > 0)
            writer.Write("enum", Options);
        if (Default != null)
            writer.Write("default", Default);
        writer.Write("description", Description);
    }
}
