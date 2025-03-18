using System;
using McpSdk.Protocol;

namespace McpSdk.Shared;

public sealed class Response : IResponse
{
    public bool IsOk { get; }
    public bool IsError { get; }
        
    public IJsonObject Result { get; }
    public ITransportError Error { get; }
        
    public Response(IJsonObject result, ITransportError error)
    {
        Result = result;
        Error = error;
        IsOk = result != null;
        IsError = error != null;
    }

    public T Unwrap<T>(Func<IJsonObject, T> onResult, Func<ITransportError, T> onError)
    {
        return IsOk ? onResult(Result) : onError(Error);
    }
        
    public static Response FromResult(IJsonObject value) => new(value, null);
    public static Response FromError(ITransportError error) => new(null, error);
}