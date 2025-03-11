using System;
using System.Text.Json;
using McpSharp.Protocol;

namespace McpSharp.Adapter.System.Text.Json;

internal sealed class JsonWriter : IJsonWriter
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

    public IJsonWriter Write(string propertyName, IJsonObject obj)
    {
        _jsonWriter.WritePropertyName(propertyName);
        _jsonWriter.WriteRawValue(obj.ToString());
        return this;
    }

    public IJsonWriter Write(string propertyName, IJsonObject[] objs)
    {
        _jsonWriter.WriteStartArray(propertyName);
        foreach (var obj in objs)
        {
            _jsonWriter.WriteRawValue(obj.ToString());
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