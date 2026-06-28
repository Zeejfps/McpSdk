namespace McpSdk.Protocol.Models;

public sealed class NumberSchema : JsonSchema
{
    public const string TypeNumber = "number";
    public const string TypeInteger = "integer";

    /// <summary>When true the schema declares <c>type: "integer"</c> instead of <c>"number"</c> (elicitation).</summary>
    public bool IsInteger { get; set; }

    /// <summary>Human-friendly display title (elicitation, 2025-11-25).</summary>
    public string Title { get; set; }

    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public string Description { get; set; }
    public double[] Options { get; set; }

    /// <summary>An optional default value clients should pre-populate (elicitation, SEP-1034).</summary>
    public double? Default { get; set; }

    public NumberSchema()
    {

    }

    public NumberSchema(IJsonObject jsonObject)
    {
        IsInteger = jsonObject["type"]?.AsString() == TypeInteger;
        Title = jsonObject["title"]?.AsString();
        Minimum = jsonObject["minimum"]?.AsDouble();
        Maximum = jsonObject["maximum"]?.AsDouble();
        Description = jsonObject["description"]?.AsString();
        Options = jsonObject["enum"]?.AsDoubleArray();
        Default = jsonObject["default"]?.AsDouble();
    }

    public override void WriteMembers(IJsonWriter writer)
    {
        writer.Write("type", IsInteger ? TypeInteger : TypeNumber);
        Title?.WriteTo(writer, "title");
        Minimum?.WriteTo(writer, "minimum");
        Maximum?.WriteTo(writer, "maximum");
        if (Options != null && Options.Length > 0)
            writer.Write("enum", Options);
        Default?.WriteTo(writer, "default");
        Description?.WriteTo(writer, "description");
    }
}
