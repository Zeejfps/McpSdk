using System.Threading.Tasks;
using McpSharp.Protocol;

namespace McpSharp.Client
{
    public interface ISamplingCapability
    {
        Task<CreateMessagesResult> CreateMessages(CreateMessageParams methodParams);
    }

    public interface ISamplingCapabilityController
    {
        Content CreateTextContent(string text);
        Content CreateImageContent(byte[] imageBytes);
        CreateMessagesResult CreateResult(string role, string model, string stopReason, Content content);
    }
}