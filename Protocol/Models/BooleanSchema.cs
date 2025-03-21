namespace McpSdk.Protocol.Models;

public sealed class BooleanSchema : JsonSchema
{
    public BooleanSchema()
    {
        
    }

    public BooleanSchema(IJsonObject jsonObject)
    {
        
    }
    
    public override void AsJson(IJsonWriter writer)
    {
        writer.Write("type", "boolean");
    }
}