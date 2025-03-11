using System;
using System.Threading;
using System.Threading.Tasks;
using McpSharp.Protocol;

namespace McpSharp.Client
{
    public interface ITransport
    {
        Task Connect(CancellationToken cancellationToken = default);
        Task SendNotification(string notification, CancellationToken cancellationToken = default);
        Task<IJsonObject> SendMessage(string method, Action<IJsonWriter> payload, CancellationToken cancellationToken = default);
    }
}