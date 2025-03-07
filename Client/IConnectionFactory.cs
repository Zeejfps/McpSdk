using System.Threading;
using System.Threading.Tasks;

namespace McpSharp.Client
{
    public interface IConnectionFactory
    {
        Task<IConnection> CreateConnection(CancellationToken cancellationToken = default);
    }
}