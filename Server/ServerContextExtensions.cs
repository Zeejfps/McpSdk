using System;
using McpSdk.Shared;

namespace McpSdk.Server
{
    /// <summary>
    /// The mutable subset of <see cref="ServerInfoOptions"/> that <c>ConfigureInfo</c> exposes. Only
    /// <see cref="Title"/> and <see cref="Description"/> are settable here: <c>Name</c> and <c>Version</c>
    /// are required and come from the <see cref="ServerBuilder"/> <c>(name, version)</c> ctor, so they are
    /// deliberately not on this interface — the signature tells the truth about what may be changed.
    /// </summary>
    public interface IServerInfoConfigurator
    {
        /// <summary>Human-friendly display name advertised during <c>initialize</c> (2025-06-18).</summary>
        string Title { get; set; }

        /// <summary>Human-friendly description advertised during <c>initialize</c> (2025-11-25).</summary>
        string Description { get; set; }
    }

    /// <summary>
    /// Server-side registration helpers for the <see cref="ServerBuilder.Context"/>. This file accumulates
    /// the <c>Add…Capability</c> registrations in later tasks; for now it carries <c>ConfigureInfo</c>.
    /// </summary>
    public static class ServerContextExtensions
    {
        /// <summary>
        /// Sets the optional <see cref="IServerInfoConfigurator.Title"/> / <see cref="IServerInfoConfigurator.Description"/>
        /// on the <see cref="ServerInfoOptions"/> singleton seeded by the <see cref="ServerBuilder"/>
        /// <c>(name, version)</c> ctor. Name and version are not configurable here. The mutation is applied to
        /// the seeded options instance in place, so the <see cref="McpSdk.Protocol.Models.ServerInfo"/>
        /// produced at resolve time picks it up; repeated calls compose. Requires a builder created with the
        /// <c>(name, version)</c> ctor.
        /// </summary>
        public static IContext ConfigureInfo(this IContext context, Action<IServerInfoConfigurator> configure)
            // ServerInfoOptions : IServerInfoConfigurator, and Action is contravariant, so the caller's
            // Action<IServerInfoConfigurator> binds directly to the Action<ServerInfoOptions> the helper wants.
            => context.ConfigureSeededOptions<ServerInfoOptions>(
                configure,
                $"ConfigureInfo requires a {nameof(ServerBuilder)} created with its (name, version) constructor.");

        /// <summary>
        /// Registers the prompts capability, served by the supplied <see cref="IPromptController"/>. The
        /// session-server factory pulls it with a null-tolerant <c>GetService</c>, so a server that never calls
        /// this does not advertise prompts.
        /// </summary>
        public static IContext AddPromptsCapability(this IContext context, IPromptController controller)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (controller == null) throw new ArgumentNullException(nameof(controller));

            context.AddSingleton<IPromptController>(controller);
            return context;
        }

        /// <summary>
        /// Registers the resources capability, served by the supplied <see cref="IResourcesController"/>. The
        /// session-server factory pulls it with a null-tolerant <c>GetService</c>, so a server that never calls
        /// this does not advertise resources.
        /// </summary>
        public static IContext AddResourcesCapability(this IContext context, IResourcesController controller)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (controller == null) throw new ArgumentNullException(nameof(controller));

            context.AddSingleton<IResourcesController>(controller);
            return context;
        }

        /// <summary>
        /// Registers the completion capability, served by the supplied <see cref="ICompletionController"/>. The
        /// session-server factory pulls it with a null-tolerant <c>GetService</c>, so a server that never calls
        /// this does not advertise completion.
        /// </summary>
        public static IContext AddCompletionCapability(this IContext context, ICompletionController controller)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (controller == null) throw new ArgumentNullException(nameof(controller));

            context.AddSingleton<ICompletionController>(controller);
            return context;
        }

        /// <summary>
        /// Enables the logging capability. Logging has no controller, so this registers a
        /// <see cref="LoggingCapabilityOptions"/> marker singleton whose <i>presence</i> tells the session-server
        /// factory to pass <c>loggingEnabled: true</c> into the <see cref="McpServer"/> ctor (implementation-plan
        /// decision #6). A server that never calls this does not advertise the logging capability.
        /// </summary>
        public static IContext AddLoggingCapability(this IContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            context.AddSingleton(new LoggingCapabilityOptions());
            return context;
        }

        /// <summary>
        /// Registers a tools capability configured through an <see cref="IToolsBuilder"/>. The builder's
        /// handlers / handler-types / page size become one <i>leaf</i> tools controller (a
        /// <see cref="DefaultToolsController"/>, its serializer resolved from the container) registered as a
        /// singleton under the internal source marker. The leaf is built (eagerly, when the provider is built)
        /// from the container it was registered in, so an <c>AddTool&lt;THandler&gt;()</c> handler is activated
        /// — with its constructor dependencies injected — from that scope. Lifetime therefore follows the
        /// registration site, not the transport: registered on the global builder <c>Context</c> the handler is
        /// one singleton shared across every session (stdio, in-memory, and HTTP alike); registered on a
        /// per-session <c>session.Context</c> (HTTP <c>ConfigureSession</c>) it is built once per session.
        /// </summary>
        /// <remarks>
        /// Each call adds another leaf rather than replacing the previous one; the public
        /// <see cref="IToolsController"/> is a composite that merges every leaf (implementation-plan decision
        /// #2). Because a leaf is only registered when this is called, a server that never calls
        /// <c>AddToolsCapability</c> resolves a <c>null</c> <see cref="IToolsController"/> and does not
        /// advertise the tools capability.
        /// </remarks>
        public static IContext AddToolsCapability(this IContext context, Action<IToolsBuilder> configure)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var builder = new ToolsBuilder();
            configure(builder);

            context.AddSingleton<IToolsControllerSource>(
                sp => new ToolsControllerSource(builder.BuildLeaf(sp), builder.PageSize));
            EnsureCompositeToolsController(context);
            return context;
        }

        /// <summary>
        /// Registers a caller-supplied <see cref="IToolsController"/> as a tools-capability leaf. Like the
        /// builder overload, this aggregates: the controller is added under the internal source marker and the
        /// public <see cref="IToolsController"/> is the composite that merges every leaf. The supplied
        /// controller does its own paging/lookup; the composite walks all of its pages when merging.
        /// </summary>
        public static IContext AddToolsCapability(this IContext context, IToolsController controller)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (controller == null) throw new ArgumentNullException(nameof(controller));

            context.AddSingleton<IToolsControllerSource>(
                _ => new ToolsControllerSource(controller, null));
            EnsureCompositeToolsController(context);
            return context;
        }

        /// <summary>
        /// Ensures the public <see cref="IToolsController"/> on <paramref name="context"/> is a
        /// <see cref="CompositeToolsController"/>, registering it (once per container) the first time a tools
        /// leaf is added. <see cref="ContextRegistrationExtensions.TryAddSingleton{TService}"/> makes a second
        /// <c>AddToolsCapability</c> on the same container a no-op here (the new leaf still aggregates via the
        /// composite's <c>GetServices</c> overlay); a per-session child container is a separate container, so
        /// it gets — and needs — its own composite.
        /// </summary>
        private static void EnsureCompositeToolsController(IContext context)
        {
            context.TryAddSingleton<IToolsController>(sp => new CompositeToolsController(sp));
        }
    }
}
