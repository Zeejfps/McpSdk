using System;
using System.Threading;
using System.Threading.Tasks;

namespace McpSdk.Client
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
        event Action Disconnected;
        Task Connect(CancellationToken cancellationToken = default);
        Task SendMessage(string endpoint, string jsonBody, CancellationToken cancellationToken = default);
        Task Disconnect();
    }
}