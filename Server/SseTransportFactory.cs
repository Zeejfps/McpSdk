using McpSdk.Protocol;

namespace McpSdk.Server;

public sealed class SseTransportFactory : ITransportFactory
{
    private readonly IJson _json;

    public SseTransportFactory(IJson json)
    {
        _json = json;
    }

    public ITransport Create()
    {
        return new SseTransport(_json);
    }
}