using System;
using McpSdk.Client.Transports;
using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Client
{
    /// <summary>
    /// Registers the stdio client transport on an <see cref="IContext"/> for the new DI builder API
    /// (<c>new ClientBuilder(name, version).Context.AddStdioTransport(command, args)</c>).
    /// </summary>
    public static class StdioClientTransportExtensions
    {
        /// <summary>
        /// Registers a singleton <see cref="ITransport"/> built directly from DI as a
        /// <see cref="StdioTransport"/> (registered directly, implementation-plan decision #4) and returns
        /// the same <see cref="IContext"/> so calls can be chained. The factory resolves
        /// <see cref="IJson"/> with <see cref="ServiceProviderExtensions.GetRequiredService{T}"/>, so a
        /// <b>missing serializer fails when the singleton is eagerly realized inside
        /// <see cref="ClientBuilder.Build"/></b> rather than later at <c>Connect()</c>. Construction is
        /// I/O-free: the <see cref="StdioTransport"/> ctor only captures the command/args; the child process
        /// is spawned in <c>OnStart</c> (i.e. at <c>Connect()</c>/<c>Start()</c>), keeping <c>Build()</c>
        /// side-effect-free.
        /// </summary>
        public static IContext AddStdioTransport(this IContext context, string command, params string[] args)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (command == null) throw new ArgumentNullException(nameof(command));

            var arguments = string.Join(" ", args ?? Array.Empty<string>());

            context.AddSingleton<ITransport>(sp =>
                new StdioTransport(command, arguments, sp.GetRequiredService<IJson>(), sp.GetService<ILoggerFactory>()));

            return context;
        }
    }
}
