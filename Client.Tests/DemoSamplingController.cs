using McpSdk.Protocol.Models;

namespace McpSdk.Client.Tests;

internal sealed class DemoSamplingController : ISamplingController
{
    public bool SupportsTools => false;

    public async Task<CreateMessagesResult> CreateMessages(CreateMessageRequest request)
    {
        var content = new TextContent("Hello world");
        return new CreateMessagesResult("asdf", "asdf", content, "wadsd");
    }
}
