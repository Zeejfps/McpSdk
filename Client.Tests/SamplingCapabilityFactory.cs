using McpSharp.Client;
using McpSharp.Protocol;

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
    public Task<ICreateMessagesResult> CreateMessages(CreateMessageParams methodParams)
    {
        throw new NotImplementedException();
    }
}