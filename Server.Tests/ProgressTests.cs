#nullable disable
using System.Linq;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server.Tests
{
    /// <summary>
    /// Progress reporting (<c>notifications/progress</c>): the server emits progress only for a request
    /// that carried a <c>_meta.progressToken</c> (keyed to that token) and emits none without one, and the
    /// client dispatches an inbound <c>notifications/progress</c> to its <c>ProgressReceived</c> handler.
    /// </summary>
    public sealed class ProgressTests : ConformanceSuite
    {
        public ProgressTests(TestReport report) : base(report) { }

        public override string Title => "Progress";

        public override async Task Run()
        {
            await Test("progress: notifications/progress emitted for a request with a progressToken", ProgressEmittedWhenTokenPresent);
            await Test("progress: no notifications/progress without a progressToken", ProgressNotEmittedWithoutToken);
            await Test("progress: client dispatches an inbound notifications/progress", ClientDispatchesProgress);
        }

        private async Task ProgressEmittedWhenTokenPresent()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildProgressServer(serverEnd);
            await server.Start();
            await clientEnd.Start();

            await clientEnd.SendRequest("tools/call", w =>
            {
                w.Write("name", "progress-tool");
                w.Write("arguments", Json.Object(_ => { }));
                w.Write("_meta", Json.Object(m => m.Write("progressToken", "p1")));
            });

            var got = await WaitUntil(() => Snapshot(clientEnd.Received).Any(m =>
            {
                var o = Json.Parse(m);
                return o["method"]?.AsString() == "notifications/progress"
                    && o["params"]?.AsObject()?["progressToken"]?.AsString() == "p1";
            }));
            Assert(got, "server emits notifications/progress keyed to the request's progressToken");
        }

        private async Task ProgressNotEmittedWithoutToken()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildProgressServer(serverEnd);
            await server.Start();
            await clientEnd.Start();

            await clientEnd.SendRequest("tools/call", w =>
            {
                w.Write("name", "progress-tool");
                w.Write("arguments", Json.Object(_ => { }));
            });

            var completed = await WaitUntil(() => Snapshot(clientEnd.Received).Any(m => Json.Parse(m)["result"] != null));
            Assert(completed, "tools/call completed");
            Assert(!Snapshot(clientEnd.Received).Any(m => Json.Parse(m)["method"]?.AsString() == "notifications/progress"),
                "no progress notification is emitted when the request carries no progressToken");
        }

        private async Task ClientDispatchesProgress()
        {
            var (client, _, serverEnd) = await ConnectedPair();

            ProgressNotification received = null;
            client.ProgressReceived += p => received = p;

            await serverEnd.SendNotification("notifications/progress",
                new ProgressNotification(new RequestId("p9"), 0.42, 1.0, "working").WriteMembers);

            var got = await WaitUntil(() => received != null);
            Assert(got, "client raises ProgressReceived for an inbound notifications/progress");
            Assert(received?.Progress == 0.42 && received.ProgressToken.StringValue == "p9",
                "progress fields round-trip to the client handler");
        }

        private IServer BuildProgressServer(InMemoryTransport serverEnd) =>
            new ServerBuilder()
                .WithName("Conf Server").WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(serverEnd))
                .WithDefaultToolsCapability(Json, SchemaCompiler, tools => tools.AddTool(new ProgressToolHandler()))
                .Build();

        private sealed class ProgressToolHandler : IToolHandler
        {
            public Tool Tool { get; } = new Tool("progress-tool", "reports progress", new ObjectSchema());

            public async Task<CallToolResult> Call(IJsonObject arguments, McpRequestContext context)
            {
                if (context != null)
                    await context.Progress.Report(0.5, 1.0, "halfway");
                return new CallToolResult(new Content[] { new TextContent("done") }, false);
            }
        }
    }
}
