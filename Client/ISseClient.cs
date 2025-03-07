using System.Threading;
using System.Threading.Tasks;

namespace McpSharp.Client
{
    public interface ISseMessage
    {
        string Id { get; }
        string Kind { get; }
        string Data { get; }
    }
    
    public interface ISseClient
    {
        Task Connect(string sseEndpoint, CancellationToken cancellationToken = default);
        Task SendMessage(string url, string jsonBody, CancellationToken cancellationToken = default);
        Task<ISseMessage> DequeueMessage(CancellationToken cancellationToken = default);
    }
}