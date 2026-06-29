namespace McpSdk.Server
{
    /// <summary>
    /// Mutable holder for the server identity advertised during <c>initialize</c>. <see cref="Name"/> and
    /// <see cref="Version"/> are seeded from the <see cref="ServerBuilder"/> <c>(name, version)</c> ctor;
    /// <see cref="Title"/> / <see cref="Description"/> are filled in later via <c>ConfigureInfo</c> (T8).
    /// Registered as a singleton in the builder's <see cref="ServerBuilder.Context"/> so the session-server
    /// factory (T9) can build a <see cref="McpSdk.Protocol.Models.ServerInfo"/> from it at resolve time.
    /// </summary>
    public sealed class ServerInfoOptions : IServerInfoConfigurator
    {
        public string Name { get; set; }
        public string Version { get; set; }

        /// <summary>Human-friendly display name (2025-06-18). Set via ConfigureInfo (T8).</summary>
        public string Title { get; set; }

        /// <summary>Human-friendly description (2025-11-25). Set via ConfigureInfo (T8).</summary>
        public string Description { get; set; }
    }
}
