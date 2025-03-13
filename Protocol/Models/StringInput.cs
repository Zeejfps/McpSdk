namespace McpSdk.Protocol.Models;

public sealed class StringInput : ToolInput
{
    public string Description { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }

    public StringInput() {}

    public StringInput(IJsonObject jsonObject)
    {
        MinLength = jsonObject["minLength"]?.AsInt();
        MaxLength = jsonObject["maxLength"]?.AsInt();
        Description = jsonObject["description"]?.AsString();
    }
    
    public override void AsJson(IJsonWriter writer)
    {
        writer.Write("type", "string");
        if (MinLength.HasValue)
            writer.Write("minLength", MinLength.Value);
        if (MaxLength.HasValue)
            writer.Write("maxLength", MaxLength.Value);
        writer.Write("description", Description);
    }
}