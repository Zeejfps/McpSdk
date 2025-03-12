using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Client
{
    public interface ISamplingCapability
    {
        Task<CreateMessagesResult> CreateMessages(CreateMessageRequest methodArguments);
    }
}