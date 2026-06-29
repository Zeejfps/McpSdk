using System;
using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Server
{
    internal static class InMemoryServerTransportExtensions
    {
        public static IContext AddInMemoryServerTransport(this IContext context, ITransport transport)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (transport == null) throw new ArgumentNullException(nameof(transport));

            context.AddSingleton<ITransport>(transport);

            context.AddSingleton<IServerHost>(sp => new SingleSessionServerHost(sp));

            context.AddServerSession();

            return context;
        }
    }
}
