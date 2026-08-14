using System.Threading;
using System.Threading.Tasks;

namespace McpSdk.Protocol
{
    public delegate void RequestReceivedCallback(JsonRpcRequest request);
    public delegate void NotificationReceivedCallback(JsonRpcNotification notification);
    public delegate void TransportClosedCallback();

    public interface ITransport
    {
        event RequestReceivedCallback RequestReceived;
        event NotificationReceivedCallback NotificationReceived;

        /// <summary>
        /// Raised once when the peer closes the wire (stdio EOF, a dropped session) rather than because
        /// <see cref="Stop"/> was called. No further messages will arrive.
        /// </summary>
        event TransportClosedCallback Closed;

        Task Start(CancellationToken cancellationToken = default);
        Task Stop();
        Task SendNotification(JsonRpcNotification notification, CancellationToken cancellationToken = default);
        Task<JsonRpcResponse> SendRequest(JsonRpcRequest request, CancellationToken cancellationToken = default);
        Task SendResponse(JsonRpcResponse response, CancellationToken cancellationToken = default);
    }
}
