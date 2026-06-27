using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol.Models;

namespace McpSdk.Protocol
{
    public delegate void RequestReceivedCallback(RequestId requestId, string method, IJsonObject arguments);
    public delegate void NotificationReceivedCallback(string notification, IJsonObject arguments);

    public interface ITransport
    {
        event RequestReceivedCallback RequestReceived;
        event NotificationReceivedCallback NotificationReceived;
        Task Start(CancellationToken cancellationToken = default);
        Task Stop();
        Task SendNotification(string notification, Json arguments = null, CancellationToken cancellationToken = default);
        Task<IResponse> SendRequest(string method, Json request, CancellationToken cancellationToken = default);
        Task SendOkResponse(RequestId requestId, Json result, CancellationToken cancellationToken = default);
        Task SendErrorResponse(RequestId requestId, Error error, CancellationToken cancellationToken = default);
    }
}