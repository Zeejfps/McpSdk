using System;
using System.Collections.Generic;

namespace McpSdk.Protocol
{
    public delegate void Json(IJsonWriter jsonWriter);
    
    public interface IJson
    {
        IJsonObject Object(Action<IJsonWriter> props);
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
        IJsonWriter Write(string propertyName, long value);
        IJsonWriter Write(string propertyName, float value);
        IJsonWriter Write(string propertyName, float[] value);
        IJsonWriter Write(string propertyName, bool value);
        IJsonWriter Write(string propertyName, bool[] value);
        IJsonWriter Write(string propertyName, IJsonObject obj);
        IJsonWriter Write(string propertyName, IJsonObject[] objs);
        IJsonWriter Write(string propertyName, Json json);
        IJsonWriter Write(string propertyName, Json[] jsonArray);
        IJsonWriter Write(string propertyName, IJsonProperty property);
    }

    public interface IJsonObject : IEnumerable<KeyValuePair<string, IJsonProperty>>
    {
        IJsonProperty this[string propertyName] { get; }
        bool IsValid(IJsonObject schema, out IList<string> errors);
        void AsJson(IJsonWriter writer);
    }

    public interface IJsonProperty
    {
        /// <summary>True when the underlying JSON value is a string (used to distinguish string ids from numeric ids).</summary>
        bool IsString { get; }
        string AsString();
        string[] AsStringArray();
        double AsDouble();
        double[] AsDoubleArray();
        int AsInt();
        int[] AsIntArray();
        long AsLong();
        float AsFloat();
        float[] AsFloatArray();
        bool AsBool();
        bool[] AsBoolArray();
        IJsonObject AsObject();
        IJsonObject[] AsObjectArray();
    }
}