#nullable disable
using System;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server.Tests
{
    /// <summary>
    /// Request cancellation (<c>notifications/cancelled</c>): cancelling the client's <c>CallTool</c> task
    /// sends the cancellation notification, which surfaces to the running server tool through its
    /// <see cref="McpRequestContext"/> cancellation token and throws <see cref="OperationCanceledException"/>
    /// back at the caller.
    /// </summary>
    public sealed class CancellationTests : ConformanceSuite
    {
        public CancellationTests(TestReport report) : base(report) { }

        public override string Title => "Cancellation";

        public override async Task Run()
        {
            await Test("cancellation: notifications/cancelled stops in-flight server work", ClientCancellationStopsServerWork);
        }

        private async Task ClientCancellationStopsServerWork()
        {
            var tool = new CancellableToolHandler();
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var builder = new ServerBuilder("Conf Server", "1.0.0");
            builder.Context.AddNewtonsoftJson();
            builder.Context.AddInMemoryServerTransport(serverEnd);
            builder.Context.AddToolsCapability(tools => tools.AddTool(tool));
            var server = builder.Build();
            await server.Start();

            var client = ConnectClient(clientEnd);
            await client.Connect();

            var cts = new CancellationTokenSource();
            var callTask = client.CallTool(new CallToolRequest("slow", Json.Object(_ => { })), cts.Token);

            var started = await WaitUntil(() => tool.Started);
            Assert(started, "server tool started running");
            cts.Cancel();

            var threw = false;
            try { await callTask; }
            catch (OperationCanceledException) { threw = true; }
            Assert(threw, "client CallTool throws OperationCanceledException when cancelled");

            var observed = await WaitUntil(() => tool.Cancelled);
            Assert(observed, "server tool observed cancellation via McpRequestContext (from notifications/cancelled)");
        }

        private sealed class CancellableToolHandler : IToolHandler
        {
            public Tool Tool { get; } = new Tool("slow", "blocks until cancelled", new ObjectSchema());
            public volatile bool Started;
            public volatile bool Cancelled;

            public async Task<CallToolResult> Call(IJsonObject arguments, McpRequestContext context)
            {
                var token = context?.CancellationToken ?? CancellationToken.None;
                Started = true;
                try
                {
                    await Task.Delay(Timeout.Infinite, token);
                }
                catch (OperationCanceledException)
                {
                    Cancelled = true;
                    throw;
                }

                return new CallToolResult(new Content[] { new TextContent("done") }, false);
            }
        }
    }
}
