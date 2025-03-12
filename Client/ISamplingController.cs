using System.Threading.Tasks;
using McpSdk.Protocol.Models;

namespace McpSdk.Client
{
    public interface ISamplingController
    {
        Task<CreateMessagesResult> CreateMessages(CreateMessageRequest request);
    }
}