namespace McpSdk.Protocol
{
    public interface ITransportFactory
    {
        ITransport Create(ILoggerFactory loggerFactory);
    }
}