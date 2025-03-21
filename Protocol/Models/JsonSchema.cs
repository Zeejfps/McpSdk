namespace McpSdk.Protocol.Models;

public abstract class JsonSchema
{
    public abstract void AsJson(IJsonWriter writer);
}