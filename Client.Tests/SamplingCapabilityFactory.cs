using McpSharp.Client;
using McpSharp.Protocol;

namespace Client.Tests;

public sealed class SamplingCapabilityFactory : ISamplingCapabilityFactory
{
    public ISamplingCapability Create(ISamplingCapabilityController controller)
    {
        return new SamplingCapability(controller);
    }
}

internal sealed class SamplingCapability : ISamplingCapability
{
    private readonly ISamplingCapabilityController _controller;

    public SamplingCapability(ISamplingCapabilityController controller)
    {
        _controller = controller;
    }

    public async Task<ICreateMessagesResult> CreateMessages(CreateMessageParams methodParams)
    {
        var content = _controller.CreateTextContent("Hello World");
        return _controller.CreateResult("asdf", "model", "asf", content);
    }
}

internal sealed class CreateMessagesResult : ICreateMessagesResult
{
    public string Role { get; }
    public string Model { get; }
    public string StopReason { get; }
    
    public Content Content { get; }
}