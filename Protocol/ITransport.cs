using System;
using System.Threading;
using System.Threading.Tasks;

namespace McpSdk.Protocol
{
    public delegate void RequestReceivedCallback(int requestId, string method, IJsonObject arguments);
    public delegate void NotificationReceivedCallback(string notification);
    
    public interface ITransport
    {
        event RequestReceivedCallback RequestReceived;
        event NotificationReceivedCallback NotificationReceived;
        Task Start(CancellationToken cancellationToken = default);
        Task SendNotification(string notification, CancellationToken cancellationToken = default);
        Task<IJsonObject> SendRequest(string method, Json request, CancellationToken cancellationToken = default);
        Task SendOkResponse(int requestId, Json result, CancellationToken cancellationToken = default);
        Task SendErrorResponse(int requestId, ErrorCode code, string message, Json data = null, CancellationToken cancellationToken = default);
    }
}