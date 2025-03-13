using System;
using McpSdk.Server;

namespace Adapter.ConsoleLogger
{
    public sealed class ConsoleLogger : ILogger
    {
        public void LogDebug(string message)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Error.WriteLine($"[D] {message}");
            Console.ResetColor();
        }

        public void LogInfo(string message)
        {
            Console.Error.WriteLine($"[I] {message}");
        }

        public void LogWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Error.WriteLine($"[W] {message}");
            Console.ResetColor();
        }

        public void LogError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"[E] {message}");
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