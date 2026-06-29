namespace McpSdk.Server
{
    /// <summary>
    /// Marker options registered by <see cref="ServerContextExtensions.AddLoggingCapability"/> to carry the
    /// "logging is enabled for this server" signal across the container. Unlike the other capabilities — each
    /// of which has a controller instance to register — logging has no controller, so its <i>presence</i> (a
    /// singleton of this type registered in the scope) is what enables it.
    /// </summary>
    /// <remarks>
    /// The session-server factory (<see cref="ServerSessionFactory.AddServerSession"/>) reads this marker with
    /// a null-tolerant <c>GetService&lt;LoggingCapabilityOptions&gt;()</c> and passes
    /// <c>loggingEnabled: marker?.Enabled == true</c> into the <see cref="McpServer"/> ctor, which only then
    /// advertises + serves the <c>logging</c> capability (implementation-plan decision #6: logging is
    /// advertised per session). A server that never calls <c>AddLoggingCapability()</c> resolves this to
    /// <c>null</c> and does not advertise logging.
    /// </remarks>
    public sealed class LoggingCapabilityOptions
    {
        /// <summary>Whether the server should advertise + serve the logging capability. Defaults to <c>true</c>.</summary>
        public bool Enabled { get; set; } = true;
    }
}
