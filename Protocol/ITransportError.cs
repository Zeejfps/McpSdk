namespace McpSdk.Protocol;

public interface ITransportError
{
    ErrorCode Code { get; }
    string Message { get; }
    IJsonObject Data { get; }
}