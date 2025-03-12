using McpSdk.Protocol.Models;

namespace McpSdk.Client.Tests;

public sealed class SamplingControllerFactory : ISamplingCapabilityFactory
{
    public ISamplingController Create()
    {
        return new SamplingController();
    }
}

internal sealed class SamplingController : ISamplingController
{
    public async Task<CreateMessagesResult> CreateMessages(CreateMessageRequest request)
    {
        var content = new TextContent("Hello world");
        return new CreateMessagesResult( "asdf", "asdf", content, "wadsd");
    }
}