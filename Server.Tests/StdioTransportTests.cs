#nullable disable
using System.Linq;
using System.Threading.Tasks;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Client;
using McpSdk.Protocol.Models;

namespace McpSdk.Server.Tests
{
    /// <summary>
    /// The stdio transport over a real OS pipe: a client <see cref="StdioTransport"/> spawns this same
    /// test assembly in <c>stdio-server</c> mode and drives the full initialize + tools/list + tools/call
    /// round-trip across a genuine process boundary — proving framing and correlation survive a real pipe,
    /// not just an in-memory channel — and stay correct under concurrent dispatch.
    /// </summary>
    public sealed class StdioTransportTests : ConformanceSuite
    {
        public StdioTransportTests(TestReport report) : base(report) { }

        public override string Title => "Stdio transport (real subprocess)";

        public override async Task Run()
        {
            await Test("real stdio round-trip: initialize + tools/list + tools/call", StdioRoundTrip);
            await Test("concurrent dispatch over stdio stays correct (write-lock smoke test)", ConcurrentDispatch);
        }

        private async Task StdioRoundTrip()
        {
            var (command, arguments) = ResolveStdioServerCommand();
            var json = new NewtonsoftJson();
            var transport = new Client.Transports.StdioTransport(command, arguments, json, Loggers);
            var client = new ClientBuilder()
                .WithName("Stdio Conf Client")
                .WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(transport))
                .Build();

            try
            {
                await WithTimeout(client.Connect(), 30000, "connect");
                Assert(client.IsConnected, "client connected to a real stdio child process");

                var tools = await WithTimeout(client.ListTools(), 15000, "tools/list");
                Assert(tools.Tools.Any(t => t.Name == "get-forecast"), "tools/list round-trips over real stdio");

                var callArgs = json.Object(w =>
                {
                    w.Write("latitude", 47.6062);
                    w.Write("longitude", -122.3321);
                    w.Write("testBool", true);
                    w.Write("testArray", new[] { "alpha", "beta" });
                });
                var result = await WithTimeout(
                    client.CallTool(new CallToolRequest("get-forecast", callArgs)), 15000, "tools/call");

                Assert(result.IsError != true, "tools/call did not report an error");
                var text = result.Content.OfType<TextContent>().FirstOrDefault()?.Text;
                Assert(text != null && text.Contains("47.6062"),
                    $"tools/call result survived the stdio round-trip (got '{text}')");
            }
            finally
            {
                try { await transport.Stop(); }
                catch { /* best-effort cleanup of the child process */ }
            }
        }

        // Smoke test for the stdio write lock, not an isolating regression guard. The server dispatches
        // each request on its own async-void path, so the responses to many concurrent calls race to the
        // single stdout StreamWriter, which the write lock serializes. This exercises that path end-to-end
        // (it would catch a mis-built lock that never releases -> deadlock -> timeout, and confirms
        // correlation holds under load). It does NOT by itself reproduce the interleaving hazard: over a
        // real pipe these small writes usually complete synchronously and never overlap, so forcing the
        // overlap deterministically would need a slow/injectable stream the transport doesn't expose.
        private async Task ConcurrentDispatch()
        {
            var (command, arguments) = ResolveStdioServerCommand();
            var json = new NewtonsoftJson();
            var transport = new Client.Transports.StdioTransport(command, arguments, json, Loggers);
            var client = new ClientBuilder()
                .WithName("Stdio Concurrency Client")
                .WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(transport))
                .Build();

            try
            {
                await WithTimeout(client.Connect(), 30000, "connect");

                CallToolRequest MakeCall() => new CallToolRequest("get-forecast", json.Object(w =>
                {
                    w.Write("latitude", 47.6062);
                    w.Write("longitude", -122.3321);
                    w.Write("testBool", true);
                    w.Write("testArray", new[] { "alpha", "beta" });
                }));

                const int count = 50;
                var calls = Enumerable.Range(0, count).Select(_ => client.CallTool(MakeCall())).ToArray();
                var results = await WithTimeout(Task.WhenAll(calls), 30000, $"{count} concurrent tools/call");

                var intact = results.Count(r => r.IsError != true
                    && r.Content.OfType<TextContent>().Any(c => c.Text != null && c.Text.Contains("47.6062")));
                Assert(intact == count, $"all {count} concurrent calls returned an intact result (got {intact})");
            }
            finally
            {
                try { await transport.Stop(); }
                catch { /* best-effort cleanup of the child process */ }
            }
        }
    }
}
