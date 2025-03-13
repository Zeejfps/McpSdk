namespace McpSdk.Protocol.Models;

public sealed class BooleanInput : ToolInput
{
    public BooleanInput()
    {
        
    }

    public BooleanInput(IJsonObject jsonObject)
    {
        
    }
    
    public override void AsJson(IJsonWriter writer)
    {
        writer.Write("type", "boolean");
    }
}