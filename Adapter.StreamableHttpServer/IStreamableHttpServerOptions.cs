using System;
using System.Collections.Generic;

namespace McpSdk.Adapter.StreamableHttpServer
{
    /// <summary>
    /// Configuration surface for <see cref="StreamableHttpServerTransportExtensions.AddStreamableHttpTransport"/>,
    /// handed to the caller's <c>configure</c> lambda. It captures two things the host needs at
    /// <c>Start()</c>: the allowed <c>Origin</c> set (the listener's DNS-rebinding guard) and the per-session
    /// <c>ConfigureSession</c> delegate. Each method returns the same instance so calls can be chained.
    /// </summary>
    public interface IStreamableHttpServerOptions
    {
        /// <summary>
        /// Registers a callback invoked once per connection, against the session's per-session
        /// <see cref="ISession.Context"/>, so the application can contribute session-scoped services
        /// (e.g. per-session tools) before that session's <c>McpServer</c> is built.
        /// </summary>
        /// <remarks>
        /// The host runs this once per connection, against <see cref="ISession.Context"/>, after registering
        /// the connection's transport into that child container and before the per-session child provider is
        /// built — so the registrations aggregate with the root's via the composite's <c>GetServices</c> overlay.
        /// </remarks>
        IStreamableHttpServerOptions ConfigureSession(Action<ISession> configure);

        /// <summary>
        /// Permits an HTTP <c>Origin</c> (DNS-rebinding guard). With no origin added the check is disabled
        /// (mirroring the listener's <c>allowedOrigins == null</c> default — appropriate for non-browser
        /// deployments). May be called multiple times to allow several origins.
        /// </summary>
        IStreamableHttpServerOptions AllowOrigin(string origin);
    }

    /// <summary>
    /// Concrete <see cref="IStreamableHttpServerOptions"/> the registration extension builds and the
    /// <see cref="StreamableHttpServerHost"/> reads. The captured state is exposed to the host via the
    /// internal <see cref="SessionConfigurator"/> / <see cref="AllowedOrigins"/> members.
    /// </summary>
    internal sealed class StreamableHttpServerOptions : IStreamableHttpServerOptions
    {
        private readonly List<string> _allowedOrigins = new List<string>();

        /// <summary>The stored per-session callback (invoked per session in T13b), or <c>null</c>.</summary>
        public Action<ISession> SessionConfigurator { get; private set; }

        /// <summary>
        /// The allowed origins to hand the listener, or <c>null</c> when none were added (which the listener
        /// reads as "origin checking disabled").
        /// </summary>
        public IEnumerable<string> AllowedOrigins => _allowedOrigins.Count == 0 ? null : _allowedOrigins;

        public IStreamableHttpServerOptions ConfigureSession(Action<ISession> configure)
        {
            SessionConfigurator = configure ?? throw new ArgumentNullException(nameof(configure));
            return this;
        }

        public IStreamableHttpServerOptions AllowOrigin(string origin)
        {
            if (origin == null) throw new ArgumentNullException(nameof(origin));
            _allowedOrigins.Add(origin);
            return this;
        }
    }
}
