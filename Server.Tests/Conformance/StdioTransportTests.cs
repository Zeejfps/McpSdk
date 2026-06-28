#nullable disable
using System.Linq;
using System.Threading.Tasks;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Client;
using McpSdk.Protocol.Models;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// The stdio transport over a real OS pipe: a client <see cref="StdioTransport"/> spawns this same
    /// test assembly in <c>stdio-server</c> mode and drives the full initialize + tools/list + tools/call
    /// round-trip across a genuine process boundary — proving framing and correlation survive a real pipe,
    /// not just an in-memory channel.
    /// </summary>
    public sealed class StdioTransportTests : ConformanceSuite
    {
        public StdioTransportTests(TestReport report) : base(report) { }

        public override string Title => "Stdio transport (real subprocess)";

        public override async Task Run()
        {
            await Test("real stdio round-trip: initialize + tools/list + tools/call", StdioRoundTrip);
        }

        private async Task StdioRoundTrip()
        {
            var (command, arguments) = ResolveStdioServerCommand();
            var json = new NewtonsoftJson();
            var transport = new McpSdk.Client.StdioTransport(command, arguments, json, Loggers);
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
    }
}
