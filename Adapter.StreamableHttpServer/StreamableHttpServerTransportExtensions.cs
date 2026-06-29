using System;
using McpSdk.Server;
using McpSdk.Shared;

namespace McpSdk.Adapter.StreamableHttpServer
{
    /// <summary>
    /// Registers the Streamable HTTP server transport on an <see cref="IContext"/> for the new DI builder API
    /// (<c>new ServerBuilder(name, version).Context.AddStreamableHttpTransport(baseUrl, path)</c>). This lives
    /// in the adapter (not core) so <c>Server</c>/<c>Shared</c> never depend on the HTTP adapter
    /// (implementation-plan decision #8).
    /// </summary>
    public static class StreamableHttpServerTransportExtensions
    {
        /// <summary>
        /// Wires a multi-session Streamable HTTP server into <paramref name="context"/>: it builds the options
        /// (running <paramref name="configure"/> to capture the per-session <c>ConfigureSession</c> callback and
        /// the allowed origins) and registers the <see cref="StreamableHttpServerHost"/> as the single
        /// <see cref="IServerHost"/> that <see cref="ServerBuilder.Build"/> resolves and returns as the
        /// <see cref="IServer"/>. The host captures the root provider (the factory's <c>sp</c>) and, per
        /// connection, builds a child scope from it. Returns the same <see cref="IContext"/> so calls can be
        /// chained.
        /// </summary>
        public static IContext AddStreamableHttpTransport(
            this IContext context,
            string baseUrl,
            string path,
            Action<IStreamableHttpServerOptions> configure = null)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (baseUrl == null) throw new ArgumentNullException(nameof(baseUrl));
            if (path == null) throw new ArgumentNullException(nameof(path));

            var options = new StreamableHttpServerOptions();
            configure?.Invoke(options);

            context.AddSingleton<IServerHost>(sp => new StreamableHttpServerHost(sp, baseUrl, path, options));

            return context;
        }
    }
}
