namespace McpSdk.Protocol.Models;

public sealed class BooleanSchema : JsonSchema
{
    public const string Type = "boolean";
    
    public string Description { get; set; }
    
    public BooleanSchema()
    {
        
    }

    public BooleanSchema(IJsonObject jsonObject)
    {
        Description = jsonObject["description"]?.AsString();
    }
    
    public override void WriteMembers(IJsonWriter writer)
    {
        writer.Write("type", Type);
        if (Description != null)
            writer.Write("description", Description);
    }
}