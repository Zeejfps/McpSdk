using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using McpSharp.Protocol;
using McpSharp.Protocol.Messages;

namespace Client.Tests;

internal class SystemJson : IJson
{

    public string Stringify(JsonRpcNotification jsonRpcNotification)
    {
        using var memoryStream = new MemoryStream();
        using var writer = new Utf8JsonWriter(memoryStream);
        
        writer.WriteStartObject();
        writer.WriteString("jsonrpc", jsonRpcNotification.JsonRpcVersion);
        writer.WriteString("method", jsonRpcNotification.Method);
        writer.WriteEndObject();
        
        writer.Flush();
        
        var jsonString = Encoding.UTF8.GetString(memoryStream.ToArray());
        return jsonString;
    }
    
    public void Parse(string jsonString, out JsonRpcResponse<int, InitializeResultPayload?> jsonRpcResponse)
    {
        // Load the JSON into a JsonDocument.
        using var document = JsonDocument.Parse(jsonString);
        var root = document.RootElement;
        
        var rpcVersion = root.GetProperty("jsonrpc").GetString();
        var id = root.GetProperty("id").GetInt32();

        InitializeResultPayload? result = null;
        if (root.TryGetProperty("result", out var resultObj))
        {
            var protocolVersion = resultObj.GetProperty("protocolVersion").GetString();
            
            var capabilitiesObj = resultObj.GetProperty("capabilities");
            var capabilities = new ServerCapabilities();
            
            if (capabilitiesObj.TryGetProperty("prompts", out var prompts))
            {
                var promptsListChanged = false;
                if (prompts.TryGetProperty("listChanged", out var listChangedProp))
                {
                    promptsListChanged = listChangedProp.GetBoolean();
                }
                capabilities.Prompts = new PromptsCapability(promptsListChanged);
            }

            if (capabilitiesObj.TryGetProperty("tools", out var toolsObj))
            {
                var toolsListChanged = false;
                if (toolsObj.TryGetProperty("listChanged", out var listChangedProp))
                    toolsListChanged = listChangedProp.GetBoolean();
                
                capabilities.Tools = new ToolsCapability(toolsListChanged);
            }
            
            var serverInfoObj = resultObj.GetProperty("serverInfo");
            var serverName = serverInfoObj.GetProperty("name").GetString();
            var serverVersion = serverInfoObj.GetProperty("version").GetString();

            var serverInfo = new ServerInfo(serverName, serverVersion);
            result = new InitializeResultPayload(protocolVersion, capabilities, serverInfo);
        }

        var error = TryParseError(root);
        jsonRpcResponse = new JsonRpcResponse<int, InitializeResultPayload?>(rpcVersion, id, result, error);
    }

    public IJsonObject Parse(string text)
    {
        var document = JsonDocument.Parse(text);
        return new JsonElementToJsonObjectAdapter(document.RootElement);
    }

    public string Stringify(Action<IJsonWriter> json)
    {
        using var memory = new MemoryStream();
        using var writer = new Utf8JsonWriter(memory);
        writer.WriteStartObject();
        json(new JsonWriter(writer));
        writer.WriteEndObject();
        writer.Flush();
        var jsonString = Encoding.UTF8.GetString(memory.ToArray());
        return jsonString;
    }
    
    private JsonRpcResponseError? TryParseError(JsonElement root)
    {
        if (!root.TryGetProperty("error", out var errorObj))
            return null;

        var code = errorObj.GetProperty("code").GetInt32();
        var message = errorObj.GetProperty("message").GetString();
        return new JsonRpcResponseError(code, message, null);
    }
}

sealed class JsonElementToJsonObjectAdapter : IJsonObject
{
    private readonly JsonElement _element;

    public JsonElementToJsonObjectAdapter(JsonElement _element)
    {
        this._element = _element;
    }

    public IJsonProperty? this[string propertyName]
    {
        get
        {
            var root = _element;
            if (root.TryGetProperty(propertyName, out var element))
            {
                return new JsonElementToJsonPropertyAdapter(element);
            }
            return null;
        }
    }

    public override string ToString()
    {
        return _element.ToString();
    }
}

sealed class JsonElementToJsonPropertyAdapter : IJsonProperty
{
    private readonly JsonElement _element;

    public JsonElementToJsonPropertyAdapter(JsonElement element)
    {
        _element = element;
    }

    public string? AsString()
    {
        return _element.GetString();
    }

    public string[] AsStringArray()
    {
        string[] array = new string?[_element.GetArrayLength()];
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = _element[i].GetString();
        }
        return array;
    }

    public double AsDouble()
    {
        return _element.GetDouble();
    }

    public double[] AsDoubleArray()
    {
        throw new NotImplementedException();
    }

    public int AsInt()
    {
        return _element.GetInt32();
    }

    public int[] AsIntArray()
    {
        throw new NotImplementedException();
    }

    public float AsFloat()
    {
        return _element.GetSingle();
    }

    public float[] AsFloatArray()
    {
        throw new NotImplementedException();
    }

    public bool AsBool()
    {
        return _element.GetBoolean();
    }

    public bool[] AsBoolArray()
    {
        throw new NotImplementedException();
    }

    public IJsonObject AsObject()
    {
        return new JsonElementToJsonObjectAdapter(_element);
    }

    public IJsonObject[] AsObjectArray()
    {
        var array = new IJsonObject[_element.GetArrayLength()];
        for (var i = 0; i < array.Length; i++)
        {
            array[i] = new JsonElementToJsonObjectAdapter(_element[i]);
        }
        return array;
    }
}

public sealed class JsonWriter : IJsonWriter
{
    private readonly Utf8JsonWriter _jsonWriter;

    public JsonWriter(Utf8JsonWriter jsonWriter)
    {
        _jsonWriter = jsonWriter;
    }

    public IJsonWriter Write(string propertyName, string value)
    {
        _jsonWriter.WriteString(propertyName, value);
        return this;
    }

    public IJsonWriter Write(string propertyName, string[] value)
    {
        _jsonWriter.WriteStartArray(propertyName);
        foreach (var element in value)
        {
            _jsonWriter.WriteStringValue(element);
        }
        _jsonWriter.WriteEndArray();
        return this;
    }

    public IJsonWriter Write(string propertyName, double value)
    {
        _jsonWriter.WriteNumber(propertyName, value);
        return this;
    }

    public IJsonWriter Write(string propertyName, double[] value)
    {
        _jsonWriter.WriteStartArray(propertyName);
        foreach (var element in value)
        {
            _jsonWriter.WriteNumberValue(element);
        }
        _jsonWriter.WriteEndArray();
        return this;
    }

    public IJsonWriter Write(string propertyName, int value)
    {
        _jsonWriter.WriteNumber(propertyName, value);
        return this;
    }

    public IJsonWriter Write(string propertyName, int[] value)
    {
        _jsonWriter.WriteStartArray(propertyName);
        foreach (var element in value)
        {
            _jsonWriter.WriteNumberValue(element);
        }
        _jsonWriter.WriteEndArray();
        return this;
    }

    public IJsonWriter Write(string propertyName, float value)
    {
        _jsonWriter.WriteNumber(propertyName, value);
        return this;
    }

    public IJsonWriter Write(string propertyName, float[] value)
    {
        _jsonWriter.WriteStartArray(propertyName);
        foreach (var element in value)
        {
            _jsonWriter.WriteNumberValue(element);
        }
        _jsonWriter.WriteEndArray();
        return this;
    }

    public IJsonWriter Write(string propertyName, bool value)
    {
        _jsonWriter.WriteBoolean(propertyName, value);
        return this;
    }

    public IJsonWriter Write(string propertyName, bool[] value)
    {
        _jsonWriter.WriteStartArray(propertyName);
        foreach (var element in value)
        {
            _jsonWriter.WriteBooleanValue(element);
        }
        _jsonWriter.WriteEndArray();
        return this;
    }

    public IJsonWriter Write(string propertyName, Action<IJsonWriter> obj)
    {
        _jsonWriter.WriteStartObject(propertyName);
        obj(this);
        _jsonWriter.WriteEndObject();
        return this;
    }

    public IJsonWriter Write(string propertyName, Action<IJsonWriter>[] objs)
    {
        _jsonWriter.WriteStartArray(propertyName);
        foreach (var obj in objs)
        {
            _jsonWriter.WriteStartObject();
            obj(this);
            _jsonWriter.WriteEndObject();
        }
        _jsonWriter.WriteEndArray();
        return this;
    }
}