using System;
using System.Linq;

namespace McpSdk.Protocol
{
    /// <summary>
    /// Extension accessors on <see cref="IJsonProperty"/> that complement its built-in <c>As*</c>
    /// readers (the read-side analogue of <see cref="JsonWriteExtensions"/>'s write helpers).
    /// </summary>
    public static class JsonPropertyExtensions
    {
        /// <summary>
        /// Maps a property holding an array of JSON objects to a typed array via <paramref name="select"/>
        /// (typically a parse-constructor, <c>o =&gt; new T(o)</c>). Null-safe on the receiver: an absent
        /// property — <c>obj["x"]</c> returning null — yields null rather than throwing, so callers can
        /// keep an optional array null (omitted on write) or coalesce to <c>Array.Empty&lt;T&gt;()</c>.
        ///
        /// A per-type <c>static abstract FromJsonObject</c> would be the "natural" alternative, but
        /// static abstract interface members need .NET 7+ and so can't live on a model interface that
        /// also targets netstandard2.0 — this factory delegate is the portable stand-in.
        /// </summary>
        public static T[] AsArray<T>(this IJsonProperty property, Func<IJsonObject, T> select)
        {
            return property?.AsObjectArray()?.Select(select).ToArray();
        }
    }
}
