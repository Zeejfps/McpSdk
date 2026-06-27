using System.Collections.Generic;
using McpSdk.Protocol.Models;

namespace McpSdk.Protocol
{
    /// <summary>
    /// Value-first counterparts to <see cref="IJsonWriter"/>'s <c>Write</c> overloads, so every value
    /// can be emitted with the same shape as <see cref="RequestId.WriteTo"/>:
    /// <c>value.WriteTo(writer, "name")</c>. This keeps envelope-building blocks visually uniform.
    /// </summary>
    public static class JsonWriteExtensions
    {
        public static void WriteTo(this string value, IJsonWriter writer, string propertyName) => writer.Write(propertyName, value);
        public static void WriteTo(this string[] value, IJsonWriter writer, string propertyName) => writer.Write(propertyName, value);
        public static void WriteTo(this double value, IJsonWriter writer, string propertyName) => writer.Write(propertyName, value);
        public static void WriteTo(this double[] value, IJsonWriter writer, string propertyName) => writer.Write(propertyName, value);
        public static void WriteTo(this int value, IJsonWriter writer, string propertyName) => writer.Write(propertyName, value);
        public static void WriteTo(this int[] value, IJsonWriter writer, string propertyName) => writer.Write(propertyName, value);
        public static void WriteTo(this long value, IJsonWriter writer, string propertyName) => writer.Write(propertyName, value);
        public static void WriteTo(this float value, IJsonWriter writer, string propertyName) => writer.Write(propertyName, value);
        public static void WriteTo(this float[] value, IJsonWriter writer, string propertyName) => writer.Write(propertyName, value);
        public static void WriteTo(this bool value, IJsonWriter writer, string propertyName) => writer.Write(propertyName, value);
        public static void WriteTo(this bool[] value, IJsonWriter writer, string propertyName) => writer.Write(propertyName, value);
        public static void WriteTo(this IJsonObject value, IJsonWriter writer, string propertyName) => writer.Write(propertyName, value);
        public static void WriteTo(this IJsonObject[] value, IJsonWriter writer, string propertyName) => writer.Write(propertyName, value);
        public static void WriteTo(this Json value, IJsonWriter writer, string propertyName) => writer.Write(propertyName, value);
        public static void WriteTo(this Json[] value, IJsonWriter writer, string propertyName) => writer.Write(propertyName, value);
        public static void WriteTo(this IJsonObjectWriter value, IJsonWriter writer, string propertyName) => writer.Write(propertyName, value);
        public static void WriteTo(this IJsonProperty value, IJsonWriter writer, string propertyName) => writer.Write(propertyName, value);
        public static void WriteTo(this IEnumerable<IJsonObjectWriter> value, IJsonWriter writer, string propertyName) => writer.Write(propertyName, value);
    }
}
