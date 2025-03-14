using System;
using McpSdk.Shared;

namespace McpSdk.Adapter.ConsoleLogger
{
    public sealed class ServerConsoleLoggerFactory : ILoggerFactory
    {
        public ILogger Create<T>()
        {
            return Create(typeof(T));
        }

        public ILogger Create(Type type)
        {
            return new ServerConsoleLogger(type);
        }
    }
}