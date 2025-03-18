using System;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Shared;

public sealed class Response : IResponse
{
    public bool IsOk { get; }
    public bool IsError { get; }
        
    public IJsonObject Result { get; }
    public Error Error { get; }
        
    public Response(IJsonObject result, Error error)
    {
        Result = result;
        Error = error;
        IsOk = result != null;
        IsError = error != null;
    }

    public T Unwrap<T>(Func<IJsonObject, T> onResult, Func<Error, T> onError)
    {
        return IsOk ? onResult(Result) : onError(Error);
    }
        
    public static Response FromResult(IJsonObject value) => new(value, null);
    public static Response FromError(Error error) => new(null, error);
}