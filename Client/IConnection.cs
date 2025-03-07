using System.Threading;
using System.Threading.Tasks;
using McpSharp.Protocol.Messages;

namespace McpSharp.Client
{
    public interface IConnection
    {
        Task<InitializeResponseMessage> SendMessage(InitializeMessage message, CancellationToken cancellationToken = default);
        Task SendMessage(InitializedMessage message, CancellationToken cancellationToken = default);
    }
}