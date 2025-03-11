using System;
using McpSharp.Protocol;
using McpSharp.Protocol.Messages;

namespace McpSharp.Client
{
    public interface IJson
    {
        string Stringify(JsonRpcRequest<int,InitializeRequestPayload> jsonRpcRequest);
        string Stringify(JsonRpcRequest<int,ListToolsRequestPayload> jsonRpcRequest);
        string Stringify(JsonRpcRequest<int,CallToolRequestPayload> jsonRpcRequest);
        string Stringify(JsonRpcNotification jsonRpcNotification);
        void Parse(string jsonString, out JsonRpcResponse<int, InitializeResultPayload> jsonRpcResponse);
        void Parse(string jsonString, out JsonRpcResponse<int, ListToolsResultPayload> jsonRpcResponse);
        void Parse(string jsonString, out JsonRpcResponse<int, CallToolResultPayload> jsonRpcResponse);

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
        IJsonWriter Write(string propertyName, Action<IJsonWriter> obj);
        IJsonWriter Write(string propertyName, Action<IJsonWriter>[] objs);
    }

    public interface IJsonObject
    {
        IJsonProperty this[string propertyName] { get; }
        bool TryGetProperty(string propertyName, out IJsonProperty property);
        IJsonProperty GetProperty(string propertyName);
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