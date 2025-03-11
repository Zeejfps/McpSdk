using System;
using System.Text.Json;
using McpSharp.Protocol;

namespace McpSharp.Adapter.System.Text.Json;

internal sealed class JsonElementToJsonPropertyAdapter : IJsonProperty
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