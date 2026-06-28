using System;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol.Models;

namespace McpSdk.Client
{
    public interface IClient
    {
        bool IsConnected { get; }

        /// <summary>Raised for each <c>notifications/message</c> log the server sends.</summary>
        event Action<LogMessage> LogMessageReceived;

        /// <summary>Raised for each <c>notifications/progress</c> update the server sends.</summary>
        event Action<ProgressNotification> ProgressReceived;

        Task Connect();
        Task Ping(CancellationToken cancellationToken = default);

        /// <summary>Sends <c>logging/setLevel</c> to ask the server for logs at or above <paramref name="level"/>.</summary>
        Task SetLoggingLevel(LoggingLevel level, CancellationToken cancellationToken = default);

        Task<ListToolsResult> ListTools(ListToolsRequest request = null, CancellationToken cancellationToken = default);
        Task<CallToolResult> CallTool(CallToolRequest request, CancellationToken cancellationToken = default);
    }
}
