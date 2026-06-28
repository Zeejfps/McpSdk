using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server;

/// <summary>
/// Per-request ambient state for the request a server controller is currently handling — its
/// cancellation token and its progress reporter. Exposed through an <see cref="AsyncLocal{T}"/> so a
/// long-running tool/resource/prompt handler can observe cancellation and report progress without the
/// controller interfaces having to thread these through every method signature.
///
/// Read it inside a handler via <see cref="Current"/>; it flows down the async call chain and is reset
/// once the request completes.
/// </summary>
public sealed class McpRequestContext
{
    private static readonly AsyncLocal<McpRequestContext> CurrentContext = new();

    /// <summary>The context of the request being handled on the current async flow, or null if none.</summary>
    public static McpRequestContext Current => CurrentContext.Value;

    internal static void SetCurrent(McpRequestContext context) => CurrentContext.Value = context;

    private readonly ITransport _transport;
    private readonly RequestId? _progressToken;

    internal McpRequestContext(CancellationToken cancellationToken, RequestId? progressToken, ITransport transport)
    {
        CancellationToken = cancellationToken;
        _progressToken = progressToken;
        _transport = transport;
    }

    /// <summary>Cancelled when the peer sends <c>notifications/cancelled</c> for this request.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>True when the caller attached a <c>progressToken</c>, so <see cref="ReportProgress"/> will emit.</summary>
    public bool IsProgressRequested => _progressToken.HasValue;

    /// <summary>
    /// Sends a <c>notifications/progress</c> update keyed to the request's progress token. A no-op when
    /// the caller did not request progress (no token), so handlers can call it unconditionally.
    /// </summary>
    public Task ReportProgress(double progress, double? total = null, string message = null)
    {
        if (!_progressToken.HasValue)
            return Task.CompletedTask;

        var note = new ProgressNotification(_progressToken.Value, progress, total, message);
        return _transport.SendNotification(new JsonRpcNotification("notifications/progress", note.WriteMembers));
    }
}
