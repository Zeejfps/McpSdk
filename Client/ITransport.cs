using System.Threading;
using System.Threading.Tasks;
using McpSharp.Protocol.Messages;

namespace McpSharp.Client
{
    public interface ITransport
    {
        Task Connect();
        Task<InitializeResponseMessage> SendMessage(InitializeMessage message, CancellationToken cancellationToken = default);
        Task<ListToolsResult> SendMessage(ListToolsRequest payload, CancellationToken cancellationToken = default);
        Task SendNotification(InitializedNotification notification, CancellationToken cancellationToken = default);
    }
}