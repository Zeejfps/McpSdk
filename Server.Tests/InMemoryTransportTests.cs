#nullable disable
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Client;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Shared;

namespace McpSdk.Server.Tests
{
    /// <summary>
    /// The in-process loopback transport (<see cref="InMemoryTransport"/>): a real
    /// <see cref="McpServer"/> ↔ <see cref="McpClient"/> pair completes the full
    /// initialize + tools/list + tools/call round-trip entirely over the shared <c>JsonRpcTransport</c>
    /// engine, with no network or child process. This is the substrate the rest of the suite is built on.
    /// Also guards the engine's request-correlation table against leaking a cancelled request.
    /// </summary>
    public sealed class InMemoryTransportTests : ConformanceSuite
    {
        public InMemoryTransportTests(TestReport report) : base(report) { }

        public override string Title => "In-memory loopback transport";

        public override async Task Run()
        {
            await Test("McpServer <-> McpClient round-trip over the loopback transport", RoundTripOverLoopback);
            await Test("a cancelled request is reclaimed from the pending table (no leak)", CancelledRequestDoesNotLeak);
        }

        private async Task RoundTripOverLoopback()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildServer(serverEnd);
            await server.Start();

            var client = ConnectClient(clientEnd);
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

        private async Task CancelledRequestDoesNotLeak()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            await clientEnd.Start();
            // serverEnd has no McpServer subscribed, so the request is never answered and stays pending
            // until we cancel it — exactly the path that used to leak its entry in the correlation table.

            using var cts = new CancellationTokenSource();
            var pending = clientEnd.SendRequest(
                new JsonRpcRequest(new RequestId(1), "tools/list", (McpSdk.Protocol.Json)(_ => { })), cts.Token);
            cts.Cancel();

            try { await pending; }
            catch (OperationCanceledException) { /* expected: the awaiter is cancelled */ }

            // White-box check: the engine must have removed the pending entry in its finally, not leaked it.
            Assert(PendingCount(clientEnd) == 0, "a cancelled request leaves no entry in the pending table");
        }

        // Reads the private correlation table on the shared JsonRpcTransport base to assert it doesn't leak.
        private static int PendingCount(JsonRpcTransport transport)
        {
            var field = typeof(JsonRpcTransport).GetField("_pending", BindingFlags.NonPublic | BindingFlags.Instance);
            return ((IDictionary)field.GetValue(transport)).Count;
        }
    }
}
