using System;

namespace McpSdk.Protocol;

public interface ILogger
{
    void LogDebug(string message);
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message);
    void LogError(Exception exception);
}

public interface ILogger<T> : ILogger {}