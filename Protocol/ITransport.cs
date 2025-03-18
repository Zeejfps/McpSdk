using System.Threading;
using System.Threading.Tasks;

namespace McpSdk.Protocol
{
    public delegate void RequestReceivedCallback(int requestId, string method, IJsonObject arguments);
    public delegate void NotificationReceivedCallback(string notification, IJsonObject arguments);
    
    public interface ITransport
    {
        event RequestReceivedCallback RequestReceived;
        event NotificationReceivedCallback NotificationReceived;
        Task Start(CancellationToken cancellationToken = default);
        Task Stop();
        Task SendNotification(string notification, Json arguments = null, CancellationToken cancellationToken = default);
        Task<IResponse> SendRequest(string method, Json request, CancellationToken cancellationToken = default);
        Task SendOkResponse(int requestId, Json result, CancellationToken cancellationToken = default);
        Task SendErrorResponse(int requestId, ErrorCode code, string message, Json data = null, CancellationToken cancellationToken = default);
    }
}