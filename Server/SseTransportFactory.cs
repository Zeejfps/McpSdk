using McpSdk.Protocol;

namespace McpSdk.Server;

public sealed class SseTransportFactory : ITransportFactory
{
    private readonly IJson _json;
    private readonly ISseConnection _sseServer;

    public SseTransportFactory(IJson json, ISseConnection sseServer)
    {
        _json = json;
        _sseServer = sseServer;
    }

    public ITransport Create()
    {
        return new SseTransport(_json, _sseServer);
    }
}