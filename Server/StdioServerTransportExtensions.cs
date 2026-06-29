using System;
using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Server
{
    /// <summary>
    /// Registers the stdio server transport on an <see cref="IContext"/> for the new DI builder API
    /// (<c>new ServerBuilder(name, version).Context.AddStdioTransport()</c>).
    /// </summary>
    public static class StdioServerTransportExtensions
    {
        /// <summary>
        /// Wires a single-session stdio server into <paramref name="context"/>. The transport is registered
        /// directly (implementation-plan decision #4); it registers three things and returns the same
        /// <see cref="IContext"/> so calls can be chained:
        /// <list type="number">
        /// <item>a singleton <see cref="ITransport"/> built directly from DI as a
        /// <see cref="StdioTransport"/>. It resolves <see cref="IJson"/> with
        /// <see cref="ServiceProviderExtensions.GetRequiredService{T}"/>, so a <b>missing serializer fails
        /// when the singleton is eagerly realized inside <see cref="ServerBuilder.Build"/></b> (the
        /// Build-validates-serializer rule) rather than later at <c>Start()</c>;</item>
        /// <item>the <see cref="IServerHost"/> (a <see cref="SingleSessionServerHost"/>) that
        /// <see cref="ServerBuilder.Build"/> resolves and returns as the <see cref="IServer"/>;</item>
        /// <item>the session <see cref="McpServer"/> (via <see cref="ServerSessionFactory.AddServerSession"/>),
        /// which pulls that same singleton <see cref="ITransport"/> from this (root) scope.</item>
        /// </list>
        /// </summary>
        public static IContext AddStdioTransport(this IContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            // (1) The wire transport, constructed directly from DI. GetRequiredService makes a missing IJson
            // throw during eager singleton realization inside Build().
            context.AddSingleton<ITransport>(sp =>
                new StdioTransport(sp.GetRequiredService<IJson>(), sp.GetService<ILoggerFactory>()));

            // (2) The lifecycle owner Build() resolves and returns as IServer.
            context.AddSingleton<IServerHost>(sp => new SingleSessionServerHost(sp));

            // (3) The single session server, which resolves the ITransport above from this same scope.
            context.AddServerSession();

            return context;
        }
    }
}
