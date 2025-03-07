using System.Threading;
using System.Threading.Tasks;

namespace McpSharp.Client
{
    public interface IConnectionFactory
    {
        IConnection CreateConnection(CancellationToken cancellationToken = default);
    }
}