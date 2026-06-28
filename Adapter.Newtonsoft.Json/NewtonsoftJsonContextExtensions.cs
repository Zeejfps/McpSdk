using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Adapter.Newtonsoft.Json
{
    public static class NewtonsoftJsonContextExtensions
    {
        /// <summary>Registers the Newtonsoft.Json implementation of <see cref="IJson"/>.</summary>
        public static IContext AddNewtonsoftJson(this IContext context)
        {
            return context.AddSingleton<IJson, NewtonsoftJson>();
        }
    }
}
