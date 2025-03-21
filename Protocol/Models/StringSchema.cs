namespace McpSdk.Protocol.Models;

public sealed class StringSchema : JsonSchema
{
    public string Description { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string[] Options { get; set; }

    public StringSchema() {}

    public StringSchema(IJsonObject jsonObject)
    {
        MinLength = jsonObject["minLength"]?.AsInt();
        MaxLength = jsonObject["maxLength"]?.AsInt();
        Description = jsonObject["description"]?.AsString();
        Options = jsonObject["enum"]?.AsStringArray();
    }
    
    public override void AsJson(IJsonWriter writer)
    {
        writer.Write("type", "string");
        if (MinLength.HasValue)
            writer.Write("minLength", MinLength.Value);
        if (MaxLength.HasValue)
            writer.Write("maxLength", MaxLength.Value);
        if (Options != null && Options.Length > 0) 
            writer.Write("enum", Options);
        writer.Write("description", Description);
    }
}