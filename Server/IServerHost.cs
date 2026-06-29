namespace McpSdk.Server
{
    /// <summary>
    /// Lifecycle owner for a server transport registered through the new <see cref="McpSdk.Shared.IContext"/>
    /// API. A transport registration (e.g. <c>AddStdioTransport</c>, added in T10) registers an
    /// <see cref="IServerHost"/> into the builder's <see cref="ServerBuilder.Context"/>;
    /// <see cref="ServerBuilder.Build"/> resolves the single <see cref="IServerHost"/> and returns it as the
    /// <see cref="IServer"/>.
    /// </summary>
    /// <remarks>
    /// Introduced minimal in T7 purely as the dual-mode <c>Build()</c> seam (the probe target that
    /// distinguishes the new API from the legacy <c>WithX</c> path). T10 added the concrete
    /// <see cref="SingleSessionServerHost"/> and its <c>AddStdioTransport</c> registration; the host owns the
    /// session lifecycle entirely through the inherited <see cref="IServer"/> members
    /// (<see cref="IServer.Start"/>/<see cref="IServer.Stop"/>/<see cref="IServer.Log"/>), so this stays a
    /// pure marker. Extend it only if a future host needs members beyond <see cref="IServer"/>.
    /// </remarks>
    public interface IServerHost : IServer
    {
    }
}
