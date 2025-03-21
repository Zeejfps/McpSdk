namespace McpSdk.Protocol.Models;

public sealed class ArraySchema : JsonSchema
{
    public string Type { get; }
    public int? MinItems { get; set; }
    public int? MaxItems { get; set; }
    public string Description { get; set; }

    public ArraySchema(string type)
    {
        Type = type;
    }

    public ArraySchema(IJsonObject jsonObject)
    {
        MinItems = jsonObject["minItems"]?.AsInt();
        MaxItems = jsonObject["maxItems"]?.AsInt();
        Type = jsonObject["items"]?.AsObject()["type"]?.AsString();
        Description = jsonObject["description"]?.AsString();
    }
    
    public override void AsJson(IJsonWriter writer)
    {
        writer.Write("type", "array");
        if (MinItems.HasValue)
            writer.Write("minItems", MinItems.Value);
        if (MaxItems.HasValue)
            writer.Write("maxItems", MaxItems.Value);
        if (Description != null)
            writer.Write("description", Description);
        writer.Write("items", itemsWriter =>
        {
            itemsWriter.Write("type", Type);
        });
    }
}