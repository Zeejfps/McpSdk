using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Adapter.StreamableHttpServer
{
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
