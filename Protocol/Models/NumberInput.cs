namespace McpSdk.Protocol.Models;

public sealed class NumberInput : ToolInput
{
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public string Description { get; set; }

    public NumberInput()
    {
        
    }

    public NumberInput(IJsonObject jsonObject)
    {
        
    }
    
    public override void AsJson(IJsonWriter writer)
    {
        writer.Write("type", "number");
        if (Minimum.HasValue)
            writer.Write("minimum", Minimum.Value);
        
        if (Maximum.HasValue)
            writer.Write("maximum", Maximum.Value);
        
        if (Description != null)
            writer.Write("description", Description);
    }
}