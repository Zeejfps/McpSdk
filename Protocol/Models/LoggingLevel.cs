using System;

namespace McpSdk.Protocol.Models;

/// <summary>
/// The eight RFC-5424 syslog severities used by MCP logging. Declared least→most severe so a
/// numerically-greater value is the more severe one — which lets the server filter emitted messages
/// against the client's set level with a single <c>(int)messageLevel &gt;= (int)setLevel</c> compare
/// (a client that sets <c>warning</c> receives warning/error/critical/alert/emergency, not info/debug).
/// </summary>
public enum LoggingLevel
{
    Debug,
    Info,
    Notice,
    Warning,
    Error,
    Critical,
    Alert,
    Emergency,
}

public static class LoggingLevelExtensions
{
    public static string ToWire(this LoggingLevel level) => level switch
    {
        LoggingLevel.Debug => "debug",
        LoggingLevel.Info => "info",
        LoggingLevel.Notice => "notice",
        LoggingLevel.Warning => "warning",
        LoggingLevel.Error => "error",
        LoggingLevel.Critical => "critical",
        LoggingLevel.Alert => "alert",
        LoggingLevel.Emergency => "emergency",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };

    public static bool TryParse(string value, out LoggingLevel level)
    {
        switch (value)
        {
            case "debug": level = LoggingLevel.Debug; return true;
            case "info": level = LoggingLevel.Info; return true;
            case "notice": level = LoggingLevel.Notice; return true;
            case "warning": level = LoggingLevel.Warning; return true;
            case "error": level = LoggingLevel.Error; return true;
            case "critical": level = LoggingLevel.Critical; return true;
            case "alert": level = LoggingLevel.Alert; return true;
            case "emergency": level = LoggingLevel.Emergency; return true;
            default: level = LoggingLevel.Info; return false;
        }
    }

    public static LoggingLevel Parse(string value) =>
        TryParse(value, out var level) ? level : throw new ArgumentException($"Unknown logging level '{value}'");
}
