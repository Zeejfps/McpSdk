using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Adapter.StreamableHttpServer
{
    /// <summary>
    /// Concrete <see cref="ISession"/> the <see cref="StreamableHttpServerHost"/> constructs once per
    /// connection. It is a plain immutable carrier — the host populates it from the per-connection
    /// transport and the fresh child container, then hands it to <c>ConfigureSession</c> before
    /// building the child provider.
    /// </summary>
    internal sealed class Session : ISession
    {
        public Session(IContext context, string sessionId, string origin, ITransport transport)
        {
            Context = context;
            SessionId = sessionId;
            Origin = origin;
            Transport = transport;
        }

        public IContext Context { get; }

        public string SessionId { get; }

        public string Origin { get; }

        public ITransport Transport { get; }
    }
}
