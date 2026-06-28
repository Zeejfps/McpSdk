using System.Threading;

namespace McpSdk.Server;

/// <summary>
/// Per-request state for the request a server controller is currently handling — its cancellation
/// token and its progress reporter. Passed explicitly into each controller/handler method so a
/// long-running tool/resource/prompt handler can observe cancellation and report progress, and so the
/// dependency is visible in the method signature rather than smuggled through ambient state.
///
/// A fresh instance is created per request and is only valid for the duration of that request.
/// </summary>
public sealed class McpRequestContext
{
    /// <summary>
    /// Constructs a request context. The server creates one per request; it is also public so handler
    /// unit tests can build one directly with a chosen <paramref name="cancellationToken"/> and a fake
    /// <paramref name="progress"/> reporter to assert on — no transport required.
    /// </summary>
    public McpRequestContext(CancellationToken cancellationToken, IProgressReporter progress)
    {
        CancellationToken = cancellationToken;
        Progress = progress;
    }

    /// <summary>Cancelled when the peer sends <c>notifications/cancelled</c> for this request.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>Reports <c>notifications/progress</c> updates for this request.</summary>
    public IProgressReporter Progress { get; }
}
