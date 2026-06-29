using System;
using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Server
{
    /// <summary>
    /// Registers a server over a <b>pre-built</b> <see cref="ITransport"/> on an <see cref="IContext"/> for the
    /// new DI builder API. This is the in-memory counterpart to
    /// <see cref="StdioServerTransportExtensions.AddStdioTransport"/>: stdio <i>creates</i> its transport from
    /// DI, whereas here the caller already holds the transport instance (the conformance suite's loopback
    /// <c>InMemoryTransport</c>) and hands it in. It is <see langword="internal"/> + surfaced to the test
    /// assembly via <c>InternalsVisibleTo</c> (see <c>Server/Properties/AssemblyInfo.cs</c>) because building a
    /// server over an externally-supplied transport is test infrastructure (implementation-plan T10b /
    /// decision #10), not part of the public surface.
    /// </summary>
    internal static class InMemoryServerTransportExtensions
    {
        /// <summary>
        /// Wires a single-session server over <paramref name="transport"/> into <paramref name="context"/>.
        /// Like <c>AddStdioTransport</c> it registers three things and returns the same <see cref="IContext"/>
        /// so calls can be chained:
        /// <list type="number">
        /// <item>the supplied <paramref name="transport"/> as the singleton <see cref="ITransport"/>
        /// <b>instance</b> (not a factory — it is already built);</item>
        /// <item>the single-session <see cref="IServerHost"/> (the same
        /// <see cref="SingleSessionServerHost"/> stdio uses) that <see cref="ServerBuilder.Build"/> resolves
        /// and returns as the <see cref="IServer"/>;</item>
        /// <item>the session <see cref="McpServer"/> (via <see cref="ServerSessionFactory.AddServerSession"/>),
        /// which pulls that same singleton <see cref="ITransport"/> from this (root) scope.</item>
        /// </list>
        /// </summary>
        public static IContext AddInMemoryServerTransport(this IContext context, ITransport transport)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (transport == null) throw new ArgumentNullException(nameof(transport));

            // (1) The pre-built wire transport, registered as the singleton instance.
            context.AddSingleton<ITransport>(transport);

            // (2) The lifecycle owner Build() resolves and returns as IServer (shared with stdio).
            context.AddSingleton<IServerHost>(sp => new SingleSessionServerHost(sp));

            // (3) The single session server, which resolves the ITransport above from this same scope.
            context.AddServerSession();

            return context;
        }
    }
}
