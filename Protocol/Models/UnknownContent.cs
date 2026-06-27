namespace McpSdk.Protocol.Models;

public sealed class UnknownContent : Content
{
    public UnknownContent(IJsonObject jsonObject)
    {
    }

    public override void WriteMembers(IJsonWriter writer)
    {
        writer.Write("type", "unknown");
    }
}