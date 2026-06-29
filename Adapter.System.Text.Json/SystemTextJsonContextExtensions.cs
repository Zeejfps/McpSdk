using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Adapter.System.Text.Json
{
    /// <summary>
    /// Registration helper that contributes the System.Text.Json-backed <see cref="IJson"/> serializer to an
    /// <see cref="IContext"/>.
    /// </summary>
    public static class SystemTextJsonContextExtensions
    {
        /// <summary>
        /// Registers <see cref="SystemJson"/> as the singleton <see cref="IJson"/> serializer.
        /// </summary>
        public static IContext AddSystemTextJson(this IContext context)
            => context.AddSingleton<IJson>(new SystemJson());
    }
}
