using System;
using System.Threading.Tasks;

namespace McpSdk.Server;

public interface ISseChannel
{
    event Action ClientConnected;
    event Action<string> MessageReceived;
    Task Send(SseEvent sseEvent);
    Task Close();
}