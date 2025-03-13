using System;

namespace McpSdk.Protocol;

public interface ILoggerFactory
{
    ILogger Create<T>();
    ILogger Create(Type type);
}