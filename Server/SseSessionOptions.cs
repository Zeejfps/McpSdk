namespace McpSdk.Server
{
    /// <summary>
    /// Options for the SSE server transport: the per-connection <see cref="ISseSession"/> the transport
    /// writes outbound messages to. Configured via <c>context.AddSseSession(session)</c>.
    /// </summary>
    public sealed class SseSessionOptions
    {
        public ISseSession Session { get; set; }
    }
}
