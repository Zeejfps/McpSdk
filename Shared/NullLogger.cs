using System;

namespace McpSdk.Shared;

internal sealed class NullLogger : ILogger
{
    public void LogDebug(string message)
    {
    }

    public void LogInfo(string message)
    {
    }

    public void LogWarning(string message)
    {
    }

    public void LogError(string message)
    {
    }

    public void LogError(Exception exception)
    {
    }
}