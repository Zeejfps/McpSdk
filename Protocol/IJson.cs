using System;

namespace McpSdk.Protocol
{
    public interface IJson
    {
        IJsonObject Build(Action<IJsonWriter> props);
        IJsonObject Parse(string text);
        string Stringify(Action<IJsonWriter> json);
    }
    
    public interface IJsonWriter
    {
        IJsonWriter Write(string propertyName, string value);
        IJsonWriter Write(string propertyName, string[] value);
        IJsonWriter Write(string propertyName, double value);
        IJsonWriter Write(string propertyName, double[] value);
        IJsonWriter Write(string propertyName, int value);
        IJsonWriter Write(string propertyName, int[] value);
        IJsonWriter Write(string propertyName, float value);
        IJsonWriter Write(string propertyName, float[] value);
        IJsonWriter Write(string propertyName, bool value);
        IJsonWriter Write(string propertyName, bool[] value);
        IJsonWriter Write(string propertyName, IJsonObject obj);
        IJsonWriter Write(string propertyName, IJsonObject[] objs);
        IJsonWriter Write(string propertyName, Action<IJsonWriter> obj);
        IJsonWriter Write(string propertyName, Action<IJsonWriter>[] objs);
    }

    public interface IJsonObject
    {
        IJsonProperty this[string propertyName] { get; }
        string ToString();
    }

    public interface IJsonProperty
    {
        string AsString();
        string[] AsStringArray();
        double AsDouble();
        double[] AsDoubleArray();
        int AsInt();
        int[] AsIntArray();
        float AsFloat();
        float[] AsFloatArray();
        bool AsBool();
        bool[] AsBoolArray();
        IJsonObject AsObject();
        IJsonObject[] AsObjectArray();
    }
}