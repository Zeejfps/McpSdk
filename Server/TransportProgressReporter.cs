using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server;

/// <summary>
/// The default <see cref="IProgressReporter"/>: emits <c>notifications/progress</c> over the transport,
/// keyed to the request's <c>progressToken</c>. When the request carried no token, every
/// <see cref="Report"/> is a no-op.
/// </summary>
internal sealed class TransportProgressReporter : IProgressReporter
{
    private readonly ITransport _transport;
    private readonly RequestId? _progressToken;

    public TransportProgressReporter(ITransport transport, RequestId? progressToken)
    {
        _transport = transport;
        _progressToken = progressToken;
    }

    public bool IsRequested => _progressToken.HasValue;

    public Task Report(double progress, double? total = null, string message = null)
    {
        if (!_progressToken.HasValue)
            return Task.CompletedTask;

        var note = new ProgressNotification(_progressToken.Value, progress, total, message);
        return _transport.SendNotification(new JsonRpcNotification("notifications/progress", note.WriteMembers));
    }
}
