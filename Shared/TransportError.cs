using McpSdk.Protocol;

namespace McpSdk.Shared;

internal sealed class TransportError : ITransportError
{
    public ErrorCode Code { get; }
    public string Message { get; }
    public IJsonObject Data { get; }
        
    public TransportError(ErrorCode code, string message, IJsonObject data)
    {
        Code = code;
        Message = message;
        Data = data;
    }
}