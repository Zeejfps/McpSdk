using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Adapter.StreamableHttpServer
{
    /// <summary>
    /// One Streamable HTTP connection (an MCP session). Each connection gets a fresh per-session
    /// <see cref="Context"/> — a child <see cref="DiContainer"/> layered over the host's root scope — into
    /// which the connection's <see cref="Transport"/> is registered before its <c>McpServer</c> is built. The
    /// host exposes this to the application's <c>ConfigureSession</c> callback so it can contribute per-session
    /// services (e.g. session-scoped tools) to <see cref="Context"/>, keyed off <see cref="SessionId"/> /
    /// <see cref="Origin"/>, before the child provider is built.
    /// </summary>
    public interface ISession
    {
        /// <summary>The per-session child container (becomes the session's child <see cref="IServiceProvider"/>).</summary>
        IContext Context { get; }

        /// <summary>The <c>Mcp-Session-Id</c> the listener issued for this connection.</summary>
        string SessionId { get; }

        /// <summary>The connection's HTTP <c>Origin</c> header, when sent; otherwise <c>null</c>.</summary>
        string Origin { get; }

        /// <summary>The per-connection transport the session's <c>McpServer</c> runs over.</summary>
        ITransport Transport { get; }
    }
}
