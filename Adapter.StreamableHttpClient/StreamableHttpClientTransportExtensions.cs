using System;
using McpSdk.Client.Transports;
using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Adapter.StreamableHttpClient
{
    /// <summary>
    /// Registers the Streamable HTTP client transport on an <see cref="IContext"/> for the new DI builder
    /// API (<c>new ClientBuilder(name, version).Context.AddStreamableHttpTransport(endpointUrl)</c>).
    /// </summary>
    public static class StreamableHttpClientTransportExtensions
    {
        /// <summary>
        /// Registers a singleton <see cref="ITransport"/> for the bare <paramref name="endpointUrl"/> and
        /// returns the same <see cref="IContext"/> so calls can be chained. The API takes a <b>string url</b>
        /// and builds the <see cref="StreamableHttpClientAdapter"/> <b>internally</b> from that url plus the DI-resolved
        /// <see cref="ILoggerFactory"/>, then constructs the <see cref="StreamableHttpTransport"/> over it.
        /// <see cref="IJson"/> is resolved with <see cref="ServiceProviderExtensions.GetRequiredService{T}"/>
        /// so a missing serializer fails when the singleton is eagerly realized inside
        /// <see cref="ClientBuilder.Build"/>. Construction is I/O-free: the adapter only creates an
        /// <c>HttpClient</c> (no connection is opened until <c>Connect()</c>/the first send), so
        /// <c>Build()</c> stays side-effect-free.
        /// </summary>
        public static IContext AddStreamableHttpTransport(this IContext context, string endpointUrl)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (endpointUrl == null) throw new ArgumentNullException(nameof(endpointUrl));

            context.AddSingleton<ITransport>(sp =>
            {
                var http = new StreamableHttpClientAdapter(endpointUrl, sp.GetService<ILoggerFactory>());
                return new StreamableHttpTransport(http, sp.GetRequiredService<IJson>(), sp.GetService<ILoggerFactory>());
            });

            return context;
        }
    }
}
