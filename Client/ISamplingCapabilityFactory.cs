namespace McpSharp.Client
{
    public interface ISamplingCapabilityFactory
    {
        ISamplingCapability Create(IClient client);
    }
}