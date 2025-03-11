using McpSharp.Client;

namespace Client.Tests;

public sealed class SamplingCapabilityFactory : ISamplingCapabilityFactory
{
    public ISamplingCapability Create(IClient client)
    {
        throw new NotImplementedException();
    }
}