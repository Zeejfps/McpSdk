using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Adapter.Newtonsoft.Json
{
    /// <summary>
    /// Registration helper that contributes the Newtonsoft.Json-backed <see cref="IJson"/> serializer to an
    /// <see cref="IContext"/>.
    /// </summary>
    public static class NewtonsoftJsonContextExtensions
    {
        /// <summary>
        /// Registers <see cref="NewtonsoftJson"/> as the singleton <see cref="IJson"/> serializer.
        /// </summary>
        public static IContext AddNewtonsoftJson(this IContext context)
            => context.AddSingleton<IJson>(new NewtonsoftJson());
    }
}
