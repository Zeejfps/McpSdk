namespace McpSdk.Server;

public interface ISseServer
{
    ISseSession CreateChannel(string messagesPath);
    void DestroyChannel(string messagePath);
}