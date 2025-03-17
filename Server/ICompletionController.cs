using System.Threading.Tasks;

namespace McpSdk.Server;

public interface ICompletionController
{
    Task<CompletionResult> Complete(CompletionRequest request);
}