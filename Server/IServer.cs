using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server
{
    public interface IServer
    {
        Task Start();
        Task Stop();

        /// <summary>
        /// Completes when the client closes the connection (on stdio, when it closes the server's stdin),
        /// when <paramref name="cancellationToken"/> is cancelled, or when <see cref="Stop"/> is called.
        /// Await this instead of blocking forever, then call <see cref="Stop"/> to drain and close.
        /// </summary>
        Task WaitForShutdown(CancellationToken cancellationToken = default);

        /// <summary>
        /// Emits a <c>notifications/message</c> log to the client, filtered by the level the client set via
        /// <c>logging/setLevel</c>. A no-op unless logging was enabled via
        /// <see cref="ServerBuilder.WithLoggingCapability"/>.
        /// </summary>
        Task Log(LoggingLevel level, Json data, string logger = null);
    }
}
