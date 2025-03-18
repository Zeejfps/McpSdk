using System;
using McpSdk.Protocol;

namespace McpSdk.Shared;

public sealed class TransportErrorException : Exception
{
    public ErrorCode Code { get; }
    public new string Message { get; }
    public new IJsonObject? Data { get; }
        
    public TransportErrorException(ErrorCode code, string message, IJsonObject data) : base($"Error ({code}): {message}")
    {
        Code = code;
        Message = message;
        Data = data;
    }
}