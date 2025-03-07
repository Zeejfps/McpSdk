namespace McpSharp.Protocol
{
    public sealed class ServerCapabilities
    {
        public LoggingCapability Logging { get; }
        public PromptsCapability Prompts { get; }
        public ResourcesCapability Resources { get; }
        public ToolsCapability Tools { get; }
    }

    public abstract class ServerCapability
    {
    }
    
    public sealed class LoggingCapability : ServerCapability
    {
    }

    public sealed class PromptsCapability : ServerCapability
    {
        public bool IsListChangedNotificationSupported { get; }
    }

    public sealed class ResourcesCapability : ServerCapability
    {
        public bool IsItemChangedNotificationSupported { get; }
        public bool IsListChangedNotificationSupported { get; }
    }

    public sealed class ToolsCapability : ServerCapability
    {
        public bool IsListChangedNotificationSupported { get; }
    }
}