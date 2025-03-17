namespace McpSdk.Protocol.Models;

public sealed class UnknownContent : Content
{
    public UnknownContent(IJsonObject jsonObject)
    {
    }

    public override void AsJson(IJsonWriter writer)
    {
        writer.Write("type", "unknown");
    }
}