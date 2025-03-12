using System;
using System.Threading.Tasks;

namespace McpSdk.Server;

public interface ISseServer
{
    event Action<string> MessageReceived;
    Task Start();
    Task Send(string json);
}