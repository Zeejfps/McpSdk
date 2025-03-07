namespace McpSharp.Protocol
{
    public sealed class ClientCapabilities
    {
        public RootsCapability Roots { get; set; }
        public SamplingCapability Sampling { get; set; }
    }

    public sealed class RootsCapability
    {
        public RootsCapability(bool isListChangedNotificationSupported)
        {
            IsListChangedNotificationSupported = isListChangedNotificationSupported;
        }

        public bool IsListChangedNotificationSupported { get; }
    }

    public sealed class SamplingCapability
    {
        
    }
}