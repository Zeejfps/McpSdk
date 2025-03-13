namespace McpSdk.Server;

public interface ISseServer
{
    ISseChannel CreateChannel(string messagesPath);
    void DestroyChannel(string messagePath);
}