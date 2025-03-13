namespace McpSdk.Protocol.Models;

public abstract class ToolInput
{
    public abstract void AsJson(IJsonWriter writer);
}