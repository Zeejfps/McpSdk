namespace McpSdk.Server;

public interface ISseServer
{
    ISseChannel CreateChannel(string connectionPath, string messagesPath);
}