using System;

namespace McpSdk.Protocol
{
    public static class JsonExtensions
    {
        /// <summary>
        /// Builds an <see cref="IJsonObject"/> from a property writer by stringifying then reparsing. A
        /// convenience composed from <see cref="IJson.Stringify"/> + <see cref="IJson.Parse"/>; kept off
        /// the <see cref="IJson"/> interface so an implementation only has to supply parse and stringify.
        /// </summary>
        public static IJsonObject Object(this IJson json, Action<IJsonWriter> props)
            => json.Parse(json.Stringify(props));
    }
}
