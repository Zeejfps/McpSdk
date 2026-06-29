using System;
using McpSdk.Protocol.Models;
using McpSdk.Shared;

namespace McpSdk.Server
{
    public sealed class ServerBuilder
    {
        private readonly DiContainer _context;

        /// <summary>
        /// The dependency-injection context. Register the transport, serializer, logger, and capabilities
        /// here (e.g. <c>builder.Context.AddStdioTransport()</c>), then call <see cref="Build"/>.
        /// </summary>
        public IContext Context => _context;

        /// <summary>
        /// <paramref name="name"/> and <paramref name="version"/> are required and seeded into
        /// <see cref="Context"/> as a <see cref="ServerInfoOptions"/> singleton, alongside a default
        /// <see cref="NullLoggerFactory"/> (overridable via <c>AddLogger</c>, last-wins).
        /// </summary>
        public ServerBuilder(string name, string version)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (version == null) throw new ArgumentNullException(nameof(version));

            _context = new DiContainer();
            _context.AddSingleton(new ServerInfoOptions { Name = name, Version = version });
            _context.AddSingleton<ILoggerFactory>(new NullLoggerFactory());

            // Produce the advertised ServerInfo at resolve time from the (mutable) options singleton, so that
            // a ConfigureInfo(...) call made after this ctor flows its Title/Description through. Name/Version
            // always come from this ctor. Resolvable even if ConfigureInfo is never called (title/desc null).
            _context.AddSingleton<ServerInfo>(sp =>
            {
                var o = sp.GetService<ServerInfoOptions>();
                return new ServerInfo(o.Name, o.Version, o.Title, o.Description);
            });
        }

        public IServer Build()
        {
            // Single DI path: build the provider and resolve the registered IServerHost. A server transport
            // registration (e.g. Context.AddStdioTransport()) puts an IServerHost into Context. Building the
            // provider here eagerly realizes its singletons, so a transport with a missing serializer surfaces
            // its wiring error here at Build() — not later at Start().
            var provider = _context.BuildServiceProvider();
            var host = provider.GetService<IServerHost>();
            if (host == null)
                throw new InvalidOperationException(
                    "No transport registered. Call Context.AddStdioTransport()/AddStreamableHttpTransport(...) before Build().");

            return host;
        }
    }
}
