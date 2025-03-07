using System;
using System.Threading;
using System.Threading.Tasks;
using McpSharp.Protocol.Messages;

namespace McpSharp.Client
{
    public interface ITransport : IDisposable
    {
        Task Connect();
        Task<InitializeResponseMessage> SendMessage(InitializeMessage message, CancellationToken cancellationToken = default);
        Task SendNotification(InitializedNotification notification, CancellationToken cancellationToken = default);
    }
}