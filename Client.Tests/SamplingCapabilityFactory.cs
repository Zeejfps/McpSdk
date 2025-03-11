using McpSharp.Client;

namespace Client.Tests;

public sealed class SamplingCapabilityFactory : ISamplingCapabilityFactory
{
    public ISamplingCapability Create()
    {
        return new SamplingCapability();
    }
}

internal sealed class SamplingCapability : ISamplingCapability
{
    
}