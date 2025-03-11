using System.Threading.Tasks;
using McpSharp.Protocol;

namespace McpSharp.Client
{
    public interface ICreateMessagesResult
    {
        string Role { get; }
        string Model { get; }
        string StopReason { get; }
    }
    
    public interface ISamplingCapability
    {
        Task<ICreateMessagesResult> CreateMessages(SamplingMessage[] messages);
    }
}