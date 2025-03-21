namespace McpSdk.Protocol.Models;

public sealed class NumberSchema : JsonSchema
{
    public const string Type = "number";
    
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public string Description { get; set; }
    public double[] Options { get; set; }

    public NumberSchema()
    {
        
    }

    public NumberSchema(IJsonObject jsonObject)
    {
        Minimum = jsonObject["minimum"]?.AsDouble();
        Maximum = jsonObject["maximum"]?.AsDouble();
        Description = jsonObject["description"]?.AsString();
        Options = jsonObject["enum"]?.AsDoubleArray();
    }
    
    public override void AsJson(IJsonWriter writer)
    {
        writer.Write("type", Type);
        
        if (Minimum.HasValue)
            writer.Write("minimum", Minimum.Value);
        
        if (Maximum.HasValue)
            writer.Write("maximum", Maximum.Value);
        
        if (Options != null && Options.Length > 0)
            writer.Write("enum", Options);
        
        if (Description != null)
            writer.Write("description", Description);
    }
}