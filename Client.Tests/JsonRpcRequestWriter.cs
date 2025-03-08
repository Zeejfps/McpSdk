using System.Text;
using System.Text.Json;
using McpSharp.Protocol;

namespace Client.Tests;

public sealed class JsonRpcRequestWriter : IDisposable, IAsyncDisposable
{
    private readonly MemoryStream _memoryStream;
    private readonly Utf8JsonWriter _jsonWriter;
    
    private JsonRpcRequestWriter(JsonRpcRequest<int> jsonRpcRequest)
    {
        _memoryStream = new MemoryStream();
        _jsonWriter = new Utf8JsonWriter(_memoryStream);
        
        var writer = _jsonWriter;
        writer.WriteStartObject();
        writer.WriteString("jsonrpc", jsonRpcRequest.JsonRpcVersion);
        writer.WriteNumber("id", jsonRpcRequest.Id);
        writer.WriteString("method", jsonRpcRequest.Method);
    }

    public static JsonRpcRequestWriter BeginWrite(JsonRpcRequest<int> jsonRpcRequest)
    {
        return new JsonRpcRequestWriter(jsonRpcRequest);
    }

    public Utf8JsonWriter WriteStartParams()
    {
        _jsonWriter.WriteStartObject("params");
        return _jsonWriter;
    }

    public void WriteEndParams()
    {
        _jsonWriter.WriteEndObject();
    }
    
    public string EndWrite()
    {
        _jsonWriter.WriteEndObject();
        _jsonWriter.Flush();
        return Encoding.UTF8.GetString(_memoryStream.ToArray());
    }

    public void Dispose()
    {
        _jsonWriter.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _jsonWriter.DisposeAsync();
    }
}