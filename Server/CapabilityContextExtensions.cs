using McpSdk.Shared;

namespace McpSdk.Server
{
    /// <summary>Server capability registration helpers for <see cref="IContext"/>.</summary>
    public static class CapabilityContextExtensions
    {
        /// <summary>Registers the tools capability controller.</summary>
        public static IContext AddToolsCapability(this IContext context, IToolsController controller)
            => context.AddSingleton<IToolsController>(controller);

        /// <summary>Registers the prompts capability controller.</summary>
        public static IContext AddPromptsCapability(this IContext context, IPromptController controller)
            => context.AddSingleton<IPromptController>(controller);

        /// <summary>Registers the resources capability controller.</summary>
        public static IContext AddResourcesCapability(this IContext context, IResourcesController controller)
            => context.AddSingleton<IResourcesController>(controller);
    }
}
