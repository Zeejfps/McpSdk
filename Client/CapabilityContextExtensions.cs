using McpSdk.Shared;

namespace McpSdk.Client
{
    /// <summary>Client capability registration helpers for <see cref="IContext"/>.</summary>
    public static class CapabilityContextExtensions
    {
        /// <summary>Registers the roots capability factory.</summary>
        public static IContext AddRootsCapability(this IContext context, IRootsCapabilityFactory capabilityFactory)
            => context.AddSingleton<IRootsCapabilityFactory>(capabilityFactory);

        /// <summary>Registers the sampling capability factory.</summary>
        public static IContext AddSamplingCapability(this IContext context, ISamplingCapabilityFactory capabilityFactory)
            => context.AddSingleton<ISamplingCapabilityFactory>(capabilityFactory);

        /// <summary>Registers the elicitation capability factory.</summary>
        public static IContext AddElicitationCapability(this IContext context, IElicitationCapabilityFactory capabilityFactory)
            => context.AddSingleton<IElicitationCapabilityFactory>(capabilityFactory);
    }
}
