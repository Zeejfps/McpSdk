namespace McpSharp.Protocol
{
    public sealed class ServerCapabilities
    {
        public LoggingCapability Logging { get; set; }
        public PromptsCapability Prompts { get; set; }
        public ResourcesCapability Resources { get; set; }
        public ToolsCapability Tools { get; set; }
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

        public PromptsCapability(bool isListChangedNotificationSupported)
        {
            IsListChangedNotificationSupported = isListChangedNotificationSupported;
        }
    }

    public sealed class ResourcesCapability : ServerCapability
    {
        public bool IsItemChangedNotificationSupported { get; }
        public bool IsListChangedNotificationSupported { get; }
    }

    public sealed class ToolsCapability : ServerCapability
    {
        public ToolsCapability(bool isListChangedNotificationSupported)
        {
            IsListChangedNotificationSupported = isListChangedNotificationSupported;
        }

        public bool IsListChangedNotificationSupported { get; }
    }
}