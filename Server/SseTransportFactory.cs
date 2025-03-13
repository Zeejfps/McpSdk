using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Server;

public sealed class SseTransportFactory : ITransportFactory
{
    private readonly IJson _json;
    private readonly ISseServer _sseServer;
    private readonly string _connectionEndpoint;
    private readonly string _messagesEndpoint;

    public SseTransportFactory(
        IJson json,
        ISseServer sseServer,
        string connectionEndpoint, string messagesEndpoint)
    {
        _json = json;
        _sseServer = sseServer;
        _connectionEndpoint = connectionEndpoint;
        _messagesEndpoint = messagesEndpoint;
    }

    public ITransport Create(ILoggerFactory loggerFactory)
    {
        return new SseTransport(_json, _sseServer, loggerFactory, _connectionEndpoint, _messagesEndpoint);
    }
}