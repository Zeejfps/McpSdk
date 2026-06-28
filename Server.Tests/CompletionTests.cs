#nullable disable
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Protocol.Models.ClientCapabilities;

namespace McpSdk.Server.Tests
{
    /// <summary>
    /// Argument completion (<c>completion/complete</c>): the request carries the 2025-11-25
    /// <c>context</c> of previously-resolved variables; a configured controller round-trips suggestions
    /// nested under <c>completion</c>; and the method returns MethodNotFound when no completion controller
    /// is wired.
    /// </summary>
    public sealed class CompletionTests : ConformanceSuite
    {
        public CompletionTests(TestReport report) : base(report) { }

        public override string Title => "Completion";

        public override async Task Run()
        {
            await Test("completion request carries context (resolved variables)", CompletionContextRoundTrips);
            await Test("completion/complete round-trips (capability + nested result)", CompletionCompleteRoundTrips);
            await Test("completion/complete -> MethodNotFound when not configured", CompletionNotConfiguredIsMethodNotFound);
        }

        private Task CompletionContextRoundTrips()
        {
            var request = new CompletionRequest(
                new PromptReference("code_review"),
                Json.Object(w => { w.Write("name", "repo"); w.Write("value", "ser"); }),
                CompletionContext.FromArguments(Json.Object(w => w.Write("owner", "octocat"))));

            var raw = Json.Object(request.WriteMembers);
            Assert(raw["context"]?.AsObject()["arguments"] != null, "completion request emits context.arguments");

            var parsed = new CompletionRequest(raw);
            Assert(parsed.Context != null, "completion context round-trips");
            AssertEqual("octocat", parsed.Context?.Arguments?["owner"]?.AsString(),
                "a previously-resolved variable round-trips in the context");

            // A context-less completion request omits the field.
            var bare = Json.Object(new CompletionRequest(
                new PromptReference("x"), Json.Object(w => w.Write("name", "a"))).WriteMembers);
            Assert(bare["context"] == null, "a context-less completion request omits context");

            return Task.CompletedTask;
        }

        private async Task CompletionCompleteRoundTrips()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = new ServerBuilder()
                .WithName("Conf Server").WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(serverEnd))
                .WithCompletionCapability(new StubCompletionController())
                .Build();
            await server.Start();
            await clientEnd.Start();

            var init = new InitializeRequest(ProtocolVersion.Latest, new ClientCapabilitiesModel(), new ClientInfo("C", "1.0.0"));
            var initResp = await clientEnd.SendRequest("initialize", init.WriteMembers);
            Assert(new InitializeResult(initResp.Result).Capabilities?.Completion != null,
                "server advertises the completion capability when a controller is configured");

            var req = new CompletionRequest(
                new PromptReference("code_review"),
                Json.Object(w => { w.Write("name", "language"); w.Write("value", "py"); }));
            var resp = await clientEnd.SendRequest("completion/complete", req.WriteMembers);

            Assert(resp.IsOk, "completion/complete returns a result");
            var completion = resp.Result?["completion"]?.AsObject();
            Assert(completion != null, "completion/complete nests suggestions under 'completion'");
            var values = completion?["values"]?.AsStringArray();
            Assert(values != null && values.Length == 2 && values[0] == "py_one", "completion values round-trip");
            Assert(completion?["total"]?.AsInt() == 2, "completion total round-trips");
        }

        private async Task CompletionNotConfiguredIsMethodNotFound()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildServer(serverEnd); // tools only, no completion controller
            await server.Start();
            await clientEnd.Start();

            var init = new InitializeRequest(ProtocolVersion.Latest, new ClientCapabilitiesModel(), new ClientInfo("C", "1.0.0"));
            var initResp = await clientEnd.SendRequest("initialize", init.WriteMembers);
            Assert(new InitializeResult(initResp.Result).Capabilities?.Completion == null,
                "completion capability is absent when no controller is configured");

            var req = new CompletionRequest(new PromptReference("x"),
                Json.Object(w => { w.Write("name", "a"); w.Write("value", "b"); }));
            var resp = await clientEnd.SendRequest("completion/complete", req.WriteMembers);
            Assert(resp.IsError && resp.Error?.Code == ErrorCode.MethodNotFound,
                "completion/complete -> MethodNotFound when not configured");
        }

        private sealed class StubCompletionController : ICompletionController
        {
            public Task<CompletionResult> Complete(CompletionRequest request, McpRequestContext context)
            {
                var prefix = request.Arguments?["value"]?.AsString() ?? "";
                return Task.FromResult(new CompletionResult(new[] { prefix + "_one", prefix + "_two" }, total: 2));
            }
        }
    }
}
