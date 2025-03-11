using McpSharp.Protocol;

namespace McpSharp.Client
{
    public interface ISamplingCapabilityController
    {
        TextContent CreateTextContent(string text);
        ImageContent CreateImageContent(string mimeType, byte[] imageBytes);
        CreateMessagesResult CreateResult(string role, string model, Content content, string stopReason);
    }
}