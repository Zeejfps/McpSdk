using System;
using System.Threading.Tasks;

namespace McpSdk.Server;

public interface ISseConnection
{
    event Action<string> MessageReceived;
    Task Start();
    Task Send(string json);
}