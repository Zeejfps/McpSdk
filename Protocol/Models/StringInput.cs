namespace McpSdk.Protocol.Models;

public sealed class StringInput : ToolInput
{
    public string Description { get; set; }

    public StringInput() {}

    public StringInput(IJsonObject jsonObject)
    {
        Description = jsonObject["description"]?.AsString();
    }
    
    public override void AsJson(IJsonWriter writer)
    {
        writer.Write("type", "string");
        writer.Write("description", Description);
    }
}