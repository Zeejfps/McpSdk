namespace McpSdk.Server;

public interface ISseServer
{
    ISseChannel GetChannel(string connectionPath, string messagesPath);
}