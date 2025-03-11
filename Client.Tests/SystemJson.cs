using System.Text;
using System.Text.Json;
using McpSharp.Protocol;

namespace Client.Tests;

internal class SystemJson : IJson
{
    public IJsonObject Build(Action<IJsonWriter> props)
    {
        var json = Stringify(props);
        return Parse(json);
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
        var array = new string?[_element.GetArrayLength()];
        for (var i = 0; i < array.Length; i++)
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
        var array = new double[_element.GetArrayLength()];
        for (var i = 0; i < array.Length; i++)
        {
            array[i] = _element[i].GetDouble();
        }
        return array;
    }

    public int AsInt()
    {
        return _element.GetInt32();
    }

    public int[] AsIntArray()
    {
        var array = new int[_element.GetArrayLength()];
        for (var i = 0; i < array.Length; i++)
        {
            array[i] = _element[i].GetInt32();
        }
        return array;
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

    public IJsonWriter Write(string propertyName, IJsonObject obj)
    {
        _jsonWriter.WritePropertyName(propertyName);
        _jsonWriter.WriteRawValue(obj.ToString());
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