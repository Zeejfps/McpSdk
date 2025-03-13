using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Server;

public sealed class SseTransportFactory : ITransportFactory
{
    private readonly IJson _json;
    private readonly ISseSession _sseSession;

    public SseTransportFactory(
        IJson json,
        ISseSession sseSession)
    {
        _json = json;
        _sseSession = sseSession;
    }

    public ITransport Create(ILoggerFactory loggerFactory)
    {
        return new SseTransport(_json, _sseSession, loggerFactory);
    }
}