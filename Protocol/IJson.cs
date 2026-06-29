using System;
using System.Collections.Generic;

namespace McpSdk.Protocol
{
    public delegate void Json(IJsonWriter jsonWriter);

    /// <summary>
    /// A model that writes itself as the members of a JSON object. <see cref="WriteMembers"/> emits
    /// only named properties; the surrounding <c>{ }</c> (or the array brackets, for the
    /// <c>IJsonObjectWriter[]</c> overload of <see cref="IJsonWriter.Write(string, IJsonObjectWriter)"/>)
    /// are supplied by the caller. That is the only possible shape: <see cref="IJsonWriter"/> exposes
    /// nothing but named-property writes, so an implementation can never emit a bare scalar or array.
    /// The nominal-typed counterpart to the <see cref="Json"/> delegate — lets callers pass the model
    /// directly (<c>writer.Write("tool", tool)</c>) instead of a method group.
    /// </summary>
    public interface IJsonObjectWriter
    {
        void WriteMembers(IJsonWriter writer);
    }

    public interface IJson
    {
        IJsonObject Parse(string text);
        string Stringify(Action<IJsonWriter> json);

        /// <summary>
        /// Compiles a schema (the members written by <paramref name="schema"/>) into a reusable
        /// validator backed by the adapter's schema engine. Compiling is the expensive step — parsing
        /// the schema text into the engine's representation — so callers should compile once and
        /// validate many times. For a typed schema model (e.g. <c>Tool.InputSchema</c>), use the
        /// <see cref="JsonExtensions.CompileSchema(IJson, IJsonObjectWriter)"/> overload, which
        /// forwards its <see cref="IJsonObjectWriter.WriteMembers"/>.
        /// </summary>
        ICompiledJsonSchema CompileSchema(Action<IJsonWriter> schema);
    }

    /// <summary>
    /// A schema parsed into the adapter engine's representation, ready to validate instances against.
    /// Obtained from <see cref="IJson.CompileSchema"/>; the engine dependency lives entirely behind
    /// this type, never on <see cref="IJsonObject"/> or the protocol schema models.
    /// </summary>
    public interface ICompiledJsonSchema
    {
        /// <summary>
        /// Validates <paramref name="data"/> against this schema. Returns true and leaves
        /// <paramref name="errors"/> null when valid; returns false with at least one message when not.
        /// </summary>
        bool Validate(IJsonObject data, out IList<string> errors);
    }

    public static class JsonExtensions
    {
        /// <summary>
        /// Compiles a typed schema model into a reusable validator by forwarding its
        /// <see cref="IJsonObjectWriter.WriteMembers"/> to <see cref="IJson.CompileSchema"/>.
        /// </summary>
        public static ICompiledJsonSchema CompileSchema(this IJson json, IJsonObjectWriter schema)
        {
            return json.CompileSchema(schema.WriteMembers);
        }
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
        IJsonWriter Write(string propertyName, IJsonObjectWriter value);
        IJsonWriter Write(string propertyName, IEnumerable<IJsonObjectWriter> values);
        IJsonWriter Write(string propertyName, IJsonProperty property);
    }

    public interface IJsonObject : IJsonObjectWriter, IEnumerable<KeyValuePair<string, IJsonProperty>>
    {
        IJsonProperty this[string propertyName] { get; }
    }

    public interface IJsonProperty
    {
        /// <summary>True when the underlying JSON value is a string (used to distinguish string ids from numeric ids).</summary>
        bool IsString { get; }
        /// <summary>True when the underlying JSON value is an array (used to distinguish a single content object from a content array).</summary>
        bool IsArray { get; }
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