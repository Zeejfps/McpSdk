using System;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Shared;

namespace McpSdk.Server
{
    /// <summary>
    /// Registers the per-connection session server (<see cref="McpServer"/>) into an
    /// <see cref="IContext"/> via a <b>factory delegate</b> (implementation-plan decision #3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="McpServer"/> cannot be type-injected by the reflection container: its constructor takes
    /// optional controllers plus a <see cref="bool"/> (<c>loggingEnabled</c>), which the greediest-ctor
    /// selection cannot satisfy from registrations alone. So it is registered with an explicit factory that
    /// pulls every dependency by hand, using <b>null-tolerant</b> <see cref="ServiceProviderExtensions.GetService{T}"/>
    /// for the four optional controllers (a capability that was never added simply resolves to <c>null</c>,
    /// which the <see cref="McpServer"/> ctor already treats as "capability absent").
    /// </para>
    /// <para>
    /// <b>Transport scoping (grounding note for T10/T10b/T13).</b> The factory resolves
    /// <see cref="ITransport"/> <i>from the scope it is resolved in</i>. The host's job is to register the
    /// session <see cref="ITransport"/> into that scope first, then resolve <see cref="McpServer"/>: stdio and
    /// the in-memory test host register one transport in the root (single session); the HTTP host registers a
    /// different transport per connection into a per-connection child scope (T13b). This helper never depends
    /// on <i>where</i> the transport lives — it just asks the provider for it.
    /// </para>
    /// <para>
    /// <b>ServerInfo.</b> Resolved (not rebuilt) so it reuses the single <see cref="ServerInfo"/> factory the
    /// builder ctor seeds from the (mutable) <see cref="ServerInfoOptions"/> singleton — which means a
    /// <c>ConfigureInfo(...)</c> call flows its Title/Description through with no duplicated construction logic.
    /// </para>
    /// <para>
    /// Called by the server hosts (T10 stdio, T10b in-memory, T13 HTTP) to wire <see cref="McpServer"/> into
    /// their scope; the host then resolves it and drives its lifecycle. The host never constructs the
    /// <see cref="McpServer"/> by hand.
    /// </para>
    /// </remarks>
    public static class ServerSessionFactory
    {
        /// <summary>
        /// Registers a singleton <see cref="McpServer"/> factory into <paramref name="context"/>. The
        /// returned <see cref="IContext"/> is the same instance, so calls can be chained.
        /// </summary>
        public static IContext AddServerSession(this IContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            context.AddSingleton<McpServer>(sp => new McpServer(
                sp.GetService<ITransport>(),
                sp.GetService<ServerInfo>(),
                sp.GetService<ILoggerFactory>(),
                sp.GetService<IToolsController>(),
                sp.GetService<IPromptController>(),
                sp.GetService<IResourcesController>(),
                sp.GetService<ICompletionController>(),
                loggingEnabled: sp.GetService<LoggingCapabilityOptions>()?.Enabled == true));

            return context;
        }
    }
}
