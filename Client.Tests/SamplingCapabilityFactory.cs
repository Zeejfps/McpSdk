using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Client.Tests;

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

    public async Task<CreateMessagesResult> CreateMessages(CreateMessageArguments methodArguments)
    {
        var content = new TextContent("Hello world");
        return new CreateMessagesResult(_json, "asdf", "asdf", content, "wadsd");
    }
}