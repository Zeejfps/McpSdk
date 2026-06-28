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

        /// <summary>
        /// Maps a property the spec types as <c>X | X[]</c> — a single object <em>or</em> an array of
        /// them — to a typed array via <paramref name="select"/>. The only field shaped this way is a
        /// sampling message's <c>content</c> (<c>SamplingMessage</c> / <c>CreateMessageResult</c>):
        /// a plain message carries one block, a tool-use / tool-result message carries several. Unlike
        /// <see cref="AsArray{T}"/>, an absent property yields an empty array (never null), since this
        /// models a present content field rather than an optional collection.
        /// </summary>
        public static T[] AsArrayOrSingle<T>(this IJsonProperty property, Func<IJsonObject, T> select)
        {
            if (property == null)
                return Array.Empty<T>();

            return property.IsArray
                ? property.AsObjectArray().Select(select).ToArray()
                : new[] { select(property.AsObject()) };
        }
    }
}
