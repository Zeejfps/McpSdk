using System.Runtime.CompilerServices;

// Surfaces the internal in-memory server-transport registration
// (InMemoryServerTransportExtensions.AddInMemoryServerTransport) to the conformance test assembly. Building a
// server over an externally-supplied ITransport is test infrastructure (implementation-plan T10b / decision
// #10), so it stays internal to the public surface and is only visible to the tests.
[assembly: InternalsVisibleTo("McpSdk.Server.Tests")]

// Surfaces the internal session server (McpServer) to the Streamable HTTP adapter host
// (StreamableHttpServerHost), which resolves it from each connection's per-session child scope and starts it
// (implementation-plan T13a). This is a one-way visibility grant only — it adds no assembly reference, so the
// dependency direction stays adapter -> core (core never depends on the HTTP adapter, decision #8).
[assembly: InternalsVisibleTo("McpSdk.Adapter.StreamableHttpServer")]
