using System.Threading;
using System.Threading.Tasks;

namespace McpSdk.Protocol
{
    public static class TransportExtensions
    {
        /// <summary>
        /// Sends a request whose <c>params</c> are supplied by a model. A convenience overload over
        /// <see cref="ITransport.SendRequest(string, Json, CancellationToken)"/> so callers can pass
        /// the model directly (<c>SendRequest("tools/list", request)</c>) instead of threading its
        /// <see cref="IJsonObjectWriter.WriteMembers"/> method group.
        /// </summary>
        public static Task<IResponse> SendRequest(
            this ITransport transport,
            string method,
            IJsonObjectWriter request,
            CancellationToken cancellationToken = default)
        {
            return transport.SendRequest(method, request.WriteMembers, cancellationToken);
        }
    }
}
