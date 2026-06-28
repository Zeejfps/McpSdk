using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Adapter.System.Text.Json
{
    public static class SystemTextJsonContextExtensions
    {
        /// <summary>Registers the System.Text.Json implementation of <see cref="IJson"/>.</summary>
        public static IContext AddSystemTextJson(this IContext context)
        {
            return context.AddSingleton<IJson, SystemJson>();
        }
    }
}
