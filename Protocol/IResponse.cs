using System;

namespace McpSdk.Protocol;

public interface IResponse
{
    bool IsOk { get; }
    bool IsError { get; }
    IJsonObject Result { get; }
    ITransportError Error { get; }
    T Unwrap<T>(Func<IJsonObject, T> onOk, Func<ITransportError, T> onError);
}