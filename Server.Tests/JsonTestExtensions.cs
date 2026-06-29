using System;
using McpSdk.Protocol;

namespace McpSdk.Server.Tests
{
    /// <summary>
    /// Test-only shorthand for materializing an <see cref="IJsonObject"/> from a writer callback.
    /// The production <see cref="IJson"/> surface intentionally omits this — callers there compose
    /// <see cref="IJson.Stringify"/> and <see cref="IJson.Parse"/> directly — but the conformance
    /// suites build ad-hoc objects often enough to justify the convenience here.
    /// </summary>
    internal static class JsonTestExtensions
    {
        public static IJsonObject Object(this IJson json, Action<IJsonWriter> props)
            => json.Parse(json.Stringify(props));
    }
}
