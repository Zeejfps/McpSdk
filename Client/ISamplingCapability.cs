using System.Threading.Tasks;
using McpSharp.Protocol;

namespace McpSharp.Client
{
    public interface ISamplingCapability
    {
        Task<CreateMessagesResult> CreateMessages(CreateMessageParams methodParams);
    }
}