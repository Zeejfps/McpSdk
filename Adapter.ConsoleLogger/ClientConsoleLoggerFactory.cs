using System;
using McpSdk.Shared;

namespace McpSdk.Adapter.ConsoleLogger
{
    public sealed class ClientConsoleLoggerFactory : ILoggerFactory 
    {
        public ILogger Create<T>()
        {
            return Create(typeof(T));
        }

        public ILogger Create(Type type)
        {
            return new ClientConsoleLogger(type);
        }
    }
}