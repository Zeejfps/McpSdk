#nullable disable
using System.Linq;
using System.Threading.Tasks;
using McpSdk.Client;
using McpSdk.Protocol.Models;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// The in-process loopback transport (<see cref="InMemoryTransport"/>): a real
    /// <see cref="McpServer"/> ↔ <see cref="McpClient"/> pair completes the full
    /// initialize + tools/list + tools/call round-trip entirely over the shared <c>JsonRpcTransport</c>
    /// engine, with no network or child process. This is the substrate the rest of the suite is built on.
    /// </summary>
    public sealed class InMemoryTransportTests : ConformanceSuite
    {
        public InMemoryTransportTests(TestReport report) : base(report) { }

        public override string Title => "In-memory loopback transport";

        public override async Task Run()
        {
            await Test("McpServer <-> McpClient round-trip over the loopback transport", RoundTripOverLoopback);
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
    }
}
