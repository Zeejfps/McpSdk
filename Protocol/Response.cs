using System;

namespace McpSdk.Protocol;

public readonly struct Response
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