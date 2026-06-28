using System.Threading.Tasks;

namespace McpSdk.Server;

/// <summary>
/// Reports incremental progress for the request currently being handled, surfaced to the peer as
/// <c>notifications/progress</c> updates. Obtained from <see cref="McpRequestContext.Progress"/>.
///
/// Exposed as an interface so a handler can be unit-tested with a fake reporter — assert on the
/// reported values — without standing up a transport.
/// </summary>
public interface IProgressReporter
{
    /// <summary>
    /// True when the caller attached a <c>progressToken</c>, so <see cref="Report"/> will emit. Handlers
    /// can use this to skip computing progress values that would be discarded.
    /// </summary>
    bool IsRequested { get; }

    /// <summary>
    /// Sends a progress update. A no-op when the caller did not request progress (no token), so handlers
    /// can call it unconditionally.
    /// </summary>
    Task Report(double progress, double? total = null, string message = null);
}
