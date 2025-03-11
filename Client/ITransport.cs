using System;
using System.Threading;
using System.Threading.Tasks;
using McpSharp.Protocol;

namespace McpSharp.Client
{
    public interface ITransport
    {
        Task Connect();
        Task SendNotification(string notificaton, CancellationToken cancellationToken = default);
        Task<IJsonObject> SendMessage(string method, Action<IJsonWriter> payload, CancellationToken cancellationToken = default);
    }
}