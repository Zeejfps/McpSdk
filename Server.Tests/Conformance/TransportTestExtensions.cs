using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// Test-only conveniences that let a test drive a bare <see cref="ITransport"/> as a request sender:
    /// they stamp a fresh request id and wrap method + params into a <see cref="JsonRpcRequest"/> /
    /// <see cref="JsonRpcNotification"/>. In production the sender (e.g. <c>McpClient</c>) owns id
    /// generation; here the test plays that role, so the counter lives with the helper.
    /// </summary>
    internal static class TransportTestExtensions
    {
        private static long _nextRequestId;

        public static Task<JsonRpcResponse> SendRequest(
            this ITransport transport,
            string method,
            McpSdk.Protocol.Json parameters,
            CancellationToken cancellationToken = default)
        {
            var id = new RequestId(Interlocked.Increment(ref _nextRequestId));
            return transport.SendRequest(new JsonRpcRequest(id, method, parameters), cancellationToken);
        }

        public static Task SendNotification(
            this ITransport transport,
            string method,
            McpSdk.Protocol.Json arguments = null,
            CancellationToken cancellationToken = default)
        {
            return transport.SendNotification(new JsonRpcNotification(method, arguments), cancellationToken);
        }
    }
}
