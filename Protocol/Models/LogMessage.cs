namespace McpSdk.Protocol.Models;

/// <summary>
/// The params of a <c>notifications/message</c> log notification: a severity <c>level</c>, an optional
/// <c>logger</c> name, and a <c>data</c> payload (any JSON; this SDK models it as an object). Outbound
/// the data is supplied as a writer; inbound it is exposed as the parsed <see cref="Data"/> object.
/// </summary>
public sealed class LogMessage : IJsonObjectWriter
{
    public LoggingLevel Level { get; }
    public string Logger { get; }

    /// <summary>The parsed <c>data</c> payload (inbound only; may be null on the outbound form).</summary>
    public IJsonObject Data { get; }

    private readonly Json _writeData;

    public LogMessage(LoggingLevel level, Json data, string logger = null)
    {
        Level = level;
        _writeData = data;
        Logger = logger;
    }

    public LogMessage(IJsonObject json)
    {
        Level = LoggingLevelExtensions.Parse(json["level"].AsString());
        Logger = json["logger"]?.AsString();
        Data = json["data"]?.AsObject();
    }

    public void WriteMembers(IJsonWriter writer)
    {
        writer.Write("level", Level.ToWire());
        if (Logger != null)
            Logger.WriteTo(writer, "logger");
        if (_writeData != null)
            writer.Write("data", _writeData);
        else if (Data != null)
            writer.Write("data", Data);
    }
}
