using System.Threading.Tasks;
using McpSdk.Protocol.Models;

namespace McpSdk.Client
{
    public interface ISamplingController
    {
        /// <summary>
        /// Whether this controller can service tool-enabled sampling requests; drives the
        /// <c>sampling.tools</c> capability advertised during initialization (2025-11-25).
        /// </summary>
        bool SupportsTools { get; }

        Task<CreateMessagesResult> CreateMessages(CreateMessageRequest request);
    }
}