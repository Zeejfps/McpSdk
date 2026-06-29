using System;
using McpSdk.Shared;

namespace McpSdk.Client
{
    /// <summary>
    /// The mutable subset of <see cref="ClientInfoOptions"/> that <c>ConfigureInfo</c> exposes. Only
    /// <see cref="Title"/> and <see cref="Description"/> are settable here: <c>Name</c> and <c>Version</c>
    /// are required and come from the <see cref="ClientBuilder"/> <c>(name, version)</c> ctor, so they are
    /// deliberately not on this interface — the signature tells the truth about what may be changed.
    /// </summary>
    public interface IClientInfoConfigurator
    {
        /// <summary>Human-friendly display name advertised during <c>initialize</c> (2025-06-18).</summary>
        string Title { get; set; }

        /// <summary>Human-friendly description advertised during <c>initialize</c> (2025-11-25).</summary>
        string Description { get; set; }
    }

    /// <summary>
    /// Client-side registration helpers for the <see cref="ClientBuilder.Context"/>. This file accumulates
    /// the <c>Add…Capability</c> registrations in later tasks; for now it carries <c>ConfigureInfo</c>.
    /// </summary>
    public static class ClientContextExtensions
    {
        /// <summary>
        /// Sets the optional <see cref="IClientInfoConfigurator.Title"/> / <see cref="IClientInfoConfigurator.Description"/>
        /// on the <see cref="ClientInfoOptions"/> singleton seeded by the <see cref="ClientBuilder"/>
        /// <c>(name, version)</c> ctor. Name and version are not configurable here. The mutation is applied to
        /// the seeded options instance in place, so the <see cref="McpSdk.Protocol.Models.ClientInfo"/>
        /// produced at resolve time picks it up; repeated calls compose. Requires a builder created with the
        /// <c>(name, version)</c> ctor.
        /// </summary>
        public static IContext ConfigureInfo(this IContext context, Action<IClientInfoConfigurator> configure)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            if (!(context is DiContainer container))
                throw new ArgumentException(
                    $"ConfigureInfo requires the {nameof(ClientBuilder)}.Context produced by a {nameof(DiContainer)}.",
                    nameof(context));

            var options = container.GetRegisteredInstance<ClientInfoOptions>();
            if (options == null)
                throw new InvalidOperationException(
                    $"ConfigureInfo requires a {nameof(ClientBuilder)} created with its (name, version) constructor.");

            configure(options);
            return context;
        }

        /// <summary>
        /// Registers the client's <see cref="IRootsController"/> so the built <see cref="McpClient"/> advertises
        /// the <c>roots</c> capability and answers server→client <c>roots/list</c> requests. The controller is
        /// registered directly (no factory indirection); <see cref="ClientBuilder.Build"/> pulls it
        /// null-tolerantly, so omitting this leaves the capability unadvertised.
        /// </summary>
        public static IContext AddRootsCapability(this IContext context, IRootsController controller)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            return context.AddSingleton<IRootsController>(controller);
        }

        /// <summary>
        /// Registers the client's <see cref="ISamplingController"/> so the built <see cref="McpClient"/> advertises
        /// the <c>sampling</c> capability and answers server→client <c>sampling/createMessage</c> requests. The
        /// controller is registered directly (no factory indirection) and pulled null-tolerantly at
        /// <see cref="ClientBuilder.Build"/>.
        /// </summary>
        public static IContext AddSamplingCapability(this IContext context, ISamplingController controller)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            return context.AddSingleton<ISamplingController>(controller);
        }

        /// <summary>
        /// Registers the client's <see cref="IElicitationController"/> so the built <see cref="McpClient"/>
        /// advertises the <c>elicitation</c> capability and answers server→client <c>elicitation/create</c>
        /// requests. The controller is registered directly (no factory indirection) and pulled null-tolerantly
        /// at <see cref="ClientBuilder.Build"/>.
        /// </summary>
        public static IContext AddElicitationCapability(this IContext context, IElicitationController controller)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            return context.AddSingleton<IElicitationController>(controller);
        }
    }
}
