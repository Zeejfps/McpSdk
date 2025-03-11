using McpSharp.Client;
using McpSharp.Protocol;

namespace Client.Tests;

public sealed class SamplingCapabilityFactory : ISamplingCapabilityFactory
{
    private readonly IJson _json;

    public SamplingCapabilityFactory(IJson json)
    {
        _json = json;
    }

    public ISamplingCapability Create()
    {
        return new SamplingCapability(_json);
    }
}

internal sealed class SamplingCapability : ISamplingCapability
{
    private readonly IJson _json;

    public SamplingCapability(IJson json)
    {
        _json = json;
    }

    public async Task<CreateMessagesResult> CreateMessages(CreateMessageParams methodParams)
    {
        var content = new TextContent(_json, "Hello world");
        return new CreateMessagesResult(_json, "asdf", "asdf", content, "wadsd");
    }
}