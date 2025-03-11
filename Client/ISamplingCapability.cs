using System.Threading.Tasks;
using McpSdk.Protocol;

namespace McpSdk.Client
{
    public interface ISamplingCapability
    {
        Task<CreateMessagesResult> CreateMessages(CreateMessageArguments methodArguments);
    }
}