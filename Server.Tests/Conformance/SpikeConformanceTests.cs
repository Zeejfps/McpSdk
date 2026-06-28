#nullable disable
using System.Linq;
using System.Threading.Tasks;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Client;
using McpSdk.Protocol.Models;
using McpSdk.Shared;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// Architecture spike: prove the three-layer seam — <see cref="IMessageChannel"/> (dumb pipe) →
    /// <see cref="JsonRpcPeer"/> (one shared correlation/dispatch engine) → <c>McpServer</c>/<c>McpClient</c>
    /// (unchanged protocol). The same <c>McpServer</c>/<c>McpClient</c> that run over the old transports
    /// run unmodified over <c>JsonRpcPeer</c>, both in-process and over a real OS stdio pipe.
    /// </summary>
    public static partial class ConformanceTests
    {
        // Test A — the layering itself: McpServer <-> McpClient entirely over JsonRpcPeer + a dumb
        // in-memory channel. Proves both sides of the protocol core are happy on the new seam.
        private static async Task PeerOverInMemoryChannel()
        {
            var (serverChannel, clientChannel) = InMemoryChannel.CreatePair();
            var serverPeer = new JsonRpcPeer(serverChannel, Json, Loggers);
            var clientPeer = new JsonRpcPeer(clientChannel, Json, Loggers);

            var server = new ServerBuilder()
                .WithName("Spike Server")
                .WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(serverPeer))
                .WithDefaultToolsCapability(Json, tools => tools.AddTool(new TestToolHandler()))
                .Build();
            await server.Start();

            var client = new ClientBuilder()
                .WithName("Spike Client")
                .WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(clientPeer))
                .Build();
            await client.Connect();

            Assert(client.IsConnected, "client connected over JsonRpcPeer + in-memory channel");

            var tools = await client.ListTools();
            Assert(tools.Tools.Any(t => t.Name == "get-forecast"), "tools/list round-trips over the channel/peer stack");

            var result = await client.CallTool(new CallToolRequest("get-forecast", Json.Object(w =>
            {
                w.Write("latitude", 47.6062);
                w.Write("longitude", -122.3321);
                w.Write("testBool", true);
                w.Write("testArray", new[] { "alpha", "beta" });
            })));
            Assert(result.IsError != true, "tools/call did not error over the channel/peer stack");
            var text = result.Content.OfType<TextContent>().FirstOrDefault()?.Text;
            Assert(text != null && text.Contains("47.6062"), $"tools/call result round-trips over the channel/peer stack (got '{text}')");
        }

        // Test B — a real OS pipe: a new-stack client (JsonRpcPeer + StdioClientChannel) drives the
        // existing stdio-server child (old stack). Proves the dumb stdio channel is wire-compatible and
        // works over a genuine process boundary, not just an in-memory toy.
        private static async Task PeerOverRealStdio()
        {
            var (command, arguments) = ResolveStdioServerCommand();
            var channel = new StdioClientChannel(command, arguments, Loggers);
            var clientPeer = new JsonRpcPeer(channel, new NewtonsoftJson(), Loggers);

            var client = new ClientBuilder()
                .WithName("Spike Stdio Client")
                .WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(clientPeer))
                .Build();

            try
            {
                await WithTimeout(client.Connect(), 30000, "connect");
                Assert(client.IsConnected, "client connected to the real stdio-server child over the channel/peer stack");

                var tools = await WithTimeout(client.ListTools(), 15000, "tools/list");
                Assert(tools.Tools.Any(t => t.Name == "get-forecast"), "tools/list round-trips over real stdio (new stack)");

                var callArgs = Json.Object(w =>
                {
                    w.Write("latitude", 47.6062);
                    w.Write("longitude", -122.3321);
                    w.Write("testBool", true);
                    w.Write("testArray", new[] { "alpha", "beta" });
                });
                var result = await WithTimeout(
                    client.CallTool(new CallToolRequest("get-forecast", callArgs)), 15000, "tools/call");
                Assert(result.IsError != true, "tools/call did not error over real stdio (new stack)");
                var text = result.Content.OfType<TextContent>().FirstOrDefault()?.Text;
                Assert(text != null && text.Contains("47.6062"), $"tools/call result survives real stdio (new stack) (got '{text}')");
            }
            finally
            {
                try { await clientPeer.Stop(); }
                catch { /* best-effort cleanup of the child process */ }
            }
        }
    }
}
