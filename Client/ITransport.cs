using System;
using System.Threading;
using System.Threading.Tasks;
using McpSharp.Protocol;
using McpSharp.Protocol.Messages;

namespace McpSharp.Client
{
    public interface ITransport
    {
        Task Connect();
        Task SendNotification(InitializedNotification notification, CancellationToken cancellationToken = default);
        Task<IJsonObject> SendMessage(string method, Action<IJsonWriter> payload, CancellationToken cancellationToken = default);
    }
}