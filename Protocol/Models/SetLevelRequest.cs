namespace McpSdk.Protocol.Models;

/// <summary>The params of a <c>logging/setLevel</c> request: the minimum severity the client wants.</summary>
public sealed class SetLevelRequest : IJsonObjectWriter
{
    public LoggingLevel Level { get; }

    public SetLevelRequest(LoggingLevel level)
    {
        Level = level;
    }

    public SetLevelRequest(IJsonObject jsonObject)
    {
        Level = LoggingLevelExtensions.Parse(jsonObject["level"].AsString());
    }

    public void WriteMembers(IJsonWriter writer)
    {
        writer.Write("level", Level.ToWire());
    }
}
