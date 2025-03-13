using System;

namespace McpSdk.Shared;

public sealed class NullLoggerFactory : ILoggerFactory
{
    public ILogger Create<T>()
    {
        return Create(typeof(T));
    }

    public ILogger Create(Type type)
    {
        return new NullLogger();
    }
}