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
    /// Architecture spike: prove that <c>McpServer</c>/<c>McpClient</c> run unmodified over the shared
    /// <see cref="JsonRpcTransport"/> engine — its correlation/dispatch is identical across transports —
    /// both in-process (the loopback <see cref="InMemoryTransport"/>) and over a real OS stdio pipe.
    /// </summary>
    public static partial class ConformanceTests
    {
        // Test A — the engine itself: McpServer <-> McpClient entirely over a loopback InMemoryTransport.
        // Proves both sides of the protocol core are happy on the shared transport base.
        private static async Task PeerOverInMemoryChannel()
        {
            var (clientPeer, serverPeer) = InMemoryTransport.CreatePair(Json, Loggers);

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

            Assert(client.IsConnected, "client connected over the loopback InMemoryTransport");

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

        // Test B — a real OS pipe: a client StdioTransport drives the stdio-server child over a genuine
        // process boundary, not just an in-memory toy.
        private static async Task PeerOverRealStdio()
        {
            var (command, arguments) = ResolveStdioServerCommand();
            var clientPeer = new McpSdk.Client.StdioTransport(command, arguments, new NewtonsoftJson(), Loggers);

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
