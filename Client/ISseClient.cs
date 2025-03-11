using System;
using System.Threading;
using System.Threading.Tasks;

namespace McpSharp.Client
{
    public interface ISseEvent
    {
        string Id { get; }
        string Kind { get; }
        string Data { get; }
    }
    
    public interface ISseClient
    {
        event Action<ISseEvent> EventReceived;
        Task Connect(string url, CancellationToken cancellationToken = default);
        Task SendMessage(string url, string jsonBody, CancellationToken cancellationToken = default);
    }
}