using System;
using System.Threading;
using System.Threading.Tasks;
using McpSharp.Protocol.Messages;

namespace McpSharp.Client
{
    public interface IConnection : IDisposable
    {
        Task<InitializeResponseMessage> SendMessage(InitializeMessage message, CancellationToken cancellationToken = default);
        Task SendNotification(InitializedNotification notification, CancellationToken cancellationToken = default);
    }
}