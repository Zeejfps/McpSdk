using McpSdk.Protocol;

namespace McpSdk.Shared
{
    public interface ITransportFactory
    {
        ITransport Create(ILoggerFactory loggerFactory);
    }
}