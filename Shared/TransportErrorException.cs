using System;
using McpSdk.Protocol.Models;

namespace McpSdk.Shared;

public sealed class TransportErrorException : Exception
{
    public Error Error { get; }
        
    public TransportErrorException(Error error) : base($"Error ({error.Code}): {error.Message}")
    {
        Error = error;
    }
}