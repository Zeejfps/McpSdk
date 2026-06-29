using System;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Shared;

namespace McpSdk.Client
{
    public sealed class ClientBuilder
    {
        private readonly DiContainer _context;

        /// <summary>
        /// The dependency-injection context. Register the transport, serializer, logger, and capabilities
        /// here (e.g. <c>builder.Context.AddStdioTransport(...)</c>), then call <see cref="Build"/>.
        /// </summary>
        public IContext Context => _context;

        /// <summary>
        /// <paramref name="name"/> and <paramref name="version"/> are required and seeded into
        /// <see cref="Context"/> as a <see cref="ClientInfoOptions"/> singleton, alongside a default
        /// <see cref="NullLoggerFactory"/> (overridable via <c>AddLogger</c>, last-wins).
        /// </summary>
        public ClientBuilder(string name, string version)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (version == null) throw new ArgumentNullException(nameof(version));

            _context = new DiContainer();
            _context.AddSingleton(new ClientInfoOptions { Name = name, Version = version });
            _context.AddSingleton<ILoggerFactory>(new NullLoggerFactory());

            _context.AddSingleton<ClientInfo>(sp =>
            {
                var o = sp.GetService<ClientInfoOptions>();
                return new ClientInfo(o.Name, o.Version, o.Title, o.Description);
            });
        }

        public IClient Build()
        {
            var provider = _context.BuildServiceProvider();
            var transport = provider.GetService<ITransport>();
            if (transport == null)
                throw new InvalidOperationException(
                    "No transport registered. Call Context.AddStdioTransport()/AddStreamableHttpTransport(...) before Build().");

            return new McpClient(
                transport,
                provider.GetService<ILoggerFactory>(),
                provider.GetService<ClientInfo>(),
                provider.GetService<IRootsController>(),
                provider.GetService<ISamplingController>(),
                provider.GetService<IElicitationController>());
        }
    }
}
