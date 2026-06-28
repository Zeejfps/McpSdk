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
        /// Emits a <c>notifications/message</c> log to the client, filtered by the level the client set via
        /// <c>logging/setLevel</c>. A no-op unless logging was enabled via
        /// <see cref="ServerBuilder.WithLoggingCapability"/>.
        /// </summary>
        Task Log(LoggingLevel level, Json data, string logger = null);
    }
}
