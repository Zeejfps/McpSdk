using System;
using System.Threading.Tasks;

namespace McpSdk.Server;

public interface ISseSession
{
    event Action<string> MessageReceived;
    string Path { get; }
    Task Send(SseEvent sseEvent);
    Task Close();
}