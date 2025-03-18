using System;
using McpSdk.Protocol.Models;

namespace McpSdk.Protocol;

public interface IResponse
{
    bool IsOk { get; }
    bool IsError { get; }
    IJsonObject Result { get; }
    Error Error { get; }
    T Unwrap<T>(Func<IJsonObject, T> onOk, Func<Error, T> onError);
}