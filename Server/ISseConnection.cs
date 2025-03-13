using System;
using System.Threading.Tasks;

namespace McpSdk.Server;

public interface ISseConnection
{
    event Action ClientConnected;
    event Action<string> MessageReceived;
    Task Send(SseEvent sseEvent);
}

public sealed class SseEvent
{
    public string Kind { get; set; }
    public string Id { get; set; }
    public string Data { get; set; }
}