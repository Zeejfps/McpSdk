using System.Threading.Tasks;

namespace McpSdk.Server.Tests
{
    /// <summary>
    /// Entry point for the conformance run. Builds every feature suite, runs them in order against one
    /// shared <see cref="TestReport"/>, and returns the number of failed assertions (the process exit code).
    ///
    /// Each suite is named for the MCP feature it exercises rather than the migration phase that first
    /// introduced it, so the suite list doubles as a coverage map of the protocol surface.
    ///
    /// Run with: <c>dotnet run --project Server.Tests -- conformance</c>
    /// </summary>
    public static class ConformanceTests
    {
        public static async Task<int> RunAll()
        {
            var report = new TestReport();

            ConformanceSuite[] suites =
            {
                // Foundation: dependency-injection container
                new DiContainerTests(report),
                new RegistrationTests(report),

                // Base protocol & lifecycle
                new ProtocolNegotiationTests(report),
                new JsonRpcFramingTests(report),
                new PingTests(report),

                // Tools
                new ToolsTests(report),
                new PaginationTests(report),

                // Shared content model
                new ContentTests(report),

                // Server-offered features
                new ResourcesTests(report),
                new PromptsTests(report),
                new CompletionTests(report),
                new LoggingTests(report),

                // Client-offered features
                new RootsTests(report),
                new SamplingTests(report),
                new ElicitationTests(report),

                // Cross-cutting request utilities
                new CancellationTests(report),
                new ProgressTests(report),

                // Transports
                new InMemoryTransportTests(report),
                new StdioTransportTests(report),
                new StreamableHttpTransportTests(report),
            };

            foreach (var suite in suites)
            {
                report.Section(suite.Title);
                await suite.Run();
            }

            return report.Summarize();
        }
    }
}
