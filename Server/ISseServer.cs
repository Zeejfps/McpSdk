namespace McpSdk.Server;

public interface ISseServer
{
    ISseConnection StartListening(string connectionPath, string messagesPath);
}