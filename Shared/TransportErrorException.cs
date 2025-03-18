using System;
using McpSdk.Protocol;

namespace McpSdk.Shared;

public sealed class TransportErrorException : Exception
{
    public ITransportError Error { get; }
        
    public TransportErrorException(ITransportError error) : base($"Error ({error.Code}): {error.Message}")
    {
        Error = error;
    }
}