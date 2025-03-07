namespace McpSharp.Client
{
    public interface ITransportFactory
    {
        ITransport Create();
    }
}