using System;
using System.Threading;
using System.Threading.Tasks;

namespace McpSharp.Protocol
{
    public delegate void RequestReceivedCallback(int messageId, string method, IJsonObject args);
    public delegate void NotificationReceivedCallback(string notification);
    
    public interface ITransport
    {
        event RequestReceivedCallback RequestReceived;
        event NotificationReceivedCallback NotificationReceived;
        Task Connect(CancellationToken cancellationToken = default);
        Task SendNotification(string notification, CancellationToken cancellationToken = default);
        Task<IJsonObject> SendRequest(string method, Action<IJsonWriter> payload, CancellationToken cancellationToken = default);
        Task SendOkResponse(int messageId, Action<IJsonWriter> payload, CancellationToken cancellationToken = default);
        Task SendErrorResponse(int messageId, Action<IJsonWriter> payload, CancellationToken cancellationToken = default);
    }
}