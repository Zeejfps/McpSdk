namespace McpSdk.Shared
{
    /// <summary>
    /// A service registration surface, modeled on Microsoft.Extensions.DependencyInjection's
    /// <c>IServiceCollection</c>. Register services with the <c>AddSingleton</c> / <c>AddTransient</c>
    /// extension methods (see <see cref="ContextRegistrationExtensions"/>), then materialize an
    /// <see cref="System.IServiceProvider"/> via <see cref="DiContainer.BuildServiceProvider"/>.
    /// </summary>
    /// <remarks>
    /// Both <c>ServerBuilder</c> and <c>ClientBuilder</c> expose an <see cref="IContext"/> so that
    /// adapters and applications can contribute services through extension methods, e.g.
    /// <c>builder.Context.AddSseTransport()</c>.
    /// </remarks>
    public interface IContext
    {
        /// <summary>
        /// Adds a service registration. The last registration for a given service type wins when the
        /// service is resolved.
        /// </summary>
        IContext Add(ServiceDescriptor descriptor);
    }
}
