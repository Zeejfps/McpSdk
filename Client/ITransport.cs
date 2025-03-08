using System.Threading;
using System.Threading.Tasks;
using McpSharp.Protocol.Messages;

namespace McpSharp.Client
{
    public interface ITransport
    {
        Task Connect();
        Task<InitializeResponsePayload> SendMessage(InitializeRequestPayload payload, CancellationToken cancellationToken = default);
        Task<ListToolsResultPayload> SendMessage(ListToolsRequestPayload payload, CancellationToken cancellationToken = default);
        Task SendNotification(InitializedNotification notification, CancellationToken cancellationToken = default);
    }
}