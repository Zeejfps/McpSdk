using System;
using McpSdk.Shared;

namespace McpSdk.Adapter.ConsoleLogger
{
    internal sealed class ServerConsoleLogger : ILogger
    {
        private readonly string _type;
        
        public ServerConsoleLogger(Type type)
        {
            _type = type.Name;
        }

        public void LogDebug(string message)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Error.WriteLine($"[D][{_type}] {message}");
            Console.ResetColor();
        }

        public void LogInfo(string message)
        {
            Console.Error.WriteLine($"[I][{_type}] {message}");
        }

        public void LogWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Error.WriteLine($"[W][{_type}] {message}");
            Console.ResetColor();
        }

        public void LogError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"[E][{_type}] {message}");
            Console.ResetColor();
        }

        public void LogError(Exception exception)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(exception);
            Console.ResetColor();
        }
    }
}