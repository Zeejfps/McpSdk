namespace McpSharp.Protocol
{
    public interface ITransportFactory
    {
        ITransport Create();
    }
}