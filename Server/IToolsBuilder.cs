namespace McpSdk.Server
{
    /// <summary>
    /// Fluent configuration surface handed to <c>AddToolsCapability(this IContext, Action&lt;IToolsBuilder&gt;)</c>.
    /// It accumulates the tool handlers (and handler types) and the optional page size that become a single
    /// <i>leaf</i> tools controller. Multiple <c>AddToolsCapability</c> calls each produce their own leaf;
    /// the public <see cref="IToolsController"/> resolved from the container is a composite that merges every
    /// leaf (see <c>CompositeToolsController</c>), so calls aggregate rather than replace.
    /// </summary>
    public interface IToolsBuilder
    {
        /// <summary>Adds a pre-built tool handler instance (shared as-is across the session).</summary>
        IToolsBuilder AddTool(IToolHandler handler);

        /// <summary>
        /// Adds a tool handler by type, container-constructed via <c>ActivatorUtilities</c> at the scope the
        /// leaf is resolved in (the root for stdio/in-memory; the per-connection child for HTTP sessions).
        /// The handler's constructor dependencies are injected from that scope, so the handler's lifetime
        /// matches the session.
        /// </summary>
        IToolsBuilder AddTool<THandler>() where THandler : class, IToolHandler;

        /// <summary>
        /// Sets the maximum number of tools returned per <c>tools/list</c> page. A value &lt;= 0 (or never
        /// calling this) means "no paging" — every tool is returned in a single page.
        /// </summary>
        IToolsBuilder WithPageSize(int pageSize);
    }
}
