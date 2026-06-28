using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Server;

public sealed class SseTransportFactory : ITransportFactory
{
    private readonly IJson _json;
    private readonly ISseSession _sseSession;
    private readonly ILoggerFactory _loggerFactory;

    public SseTransportFactory(
        IJson json,
        ISseSession sseSession,
        ILoggerFactory loggerFactory)
    {
        _json = json;
        _sseSession = sseSession;
        _loggerFactory = loggerFactory;
    }

    public ITransport Create()
    {
        return new SseTransport(_json, _sseSession, _loggerFactory);
    }
}