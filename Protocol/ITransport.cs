using System.Threading;
using System.Threading.Tasks;

namespace McpSdk.Protocol
{
    public delegate void RequestReceivedCallback(JsonRpcRequest request);
    public delegate void NotificationReceivedCallback(JsonRpcNotification notification);

    public interface ITransport
    {
        event RequestReceivedCallback RequestReceived;
        event NotificationReceivedCallback NotificationReceived;
        Task Start(CancellationToken cancellationToken = default);
        Task Stop();
        Task SendNotification(string notification, Json arguments = null, CancellationToken cancellationToken = default);
        Task<JsonRpcResponse> SendRequest(string method, Json request, CancellationToken cancellationToken = default);
        Task SendResponse(JsonRpcResponse response, CancellationToken cancellationToken = default);
    }
}
