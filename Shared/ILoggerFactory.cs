using System;

namespace McpSdk.Shared;

public interface ILoggerFactory
{
    ILogger Create<T>();
    ILogger Create(Type type);
}