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
    
    public override void WriteMembers(IJsonWriter writer)
    {
        writer.Write("type", "array");
        MinItems?.WriteTo(writer, "minItems");
        MaxItems?.WriteTo(writer, "maxItems");
        Description?.WriteTo(writer, "description");
        writer.Write("items", itemsWriter =>
        {
            itemsWriter.Write("type", Type);
        });
    }
}