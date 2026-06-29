#nullable disable
using System;
using System.Linq;
using System.Threading.Tasks;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Protocol.Models.ClientCapabilities;

namespace McpSdk.Server.Tests
{
    /// <summary>
    /// Prompts, end to end and at the model layer: <see cref="Prompt"/> (with its
    /// <see cref="PromptArgument"/>s) and <see cref="PromptMessage"/> carry title / arguments / icons /
    /// _meta through a listing; a <c>prompts/get</c> request round-trips its name + arguments; and a real
    /// server serves <c>prompts/list</c> + <c>prompts/get</c> and emits <c>prompts/list_changed</c>.
    /// </summary>
    public sealed class PromptsTests : ConformanceSuite
    {
        public PromptsTests(TestReport report) : base(report) { }

        public override string Title => "Prompts";

        public override async Task Run()
        {
            await Test("prompts/list carries prompts with title + arguments + icons + _meta", PromptListingRoundTrips);
            await Test("prompts/get carries messages + description + message _meta", GetPromptResultRoundTrips);
            await Test("prompts/get request round-trips name + arguments", GetPromptRequestReadsArguments);
            await Test("prompts/list round-trips prompts through the server", ListPromptsThroughServer);
            await Test("prompts/get round-trips messages + arguments through the server", GetPromptThroughServer);
            await Test("notifications/prompts/list_changed reaches the client", PromptsListChangedNotification);
        }

        // -- Model round-trips ---------------------------------------------------------------

        private Task PromptListingRoundTrips()
        {
            var prompt = new Prompt("code_review", "Review a pull request",
                new[]
                {
                    new PromptArgument("pr_url", "URL of the PR", required: true) { Title = "PR URL" },
                    new PromptArgument("tone", "Reviewer tone"),
                })
            {
                Title = "Code Review",
                Icons = new[] { new Icon("https://example.com/review.svg") },
                Meta = new Meta(Json.Object(w => w.Write("category", "dev"))),
            };

            var result = new ListPromptsResult(new[] { prompt }, "next-page");
            var raw = Json.Object(result.WriteMembers);

            AssertEqual("next-page", raw["nextCursor"]?.AsString(), "prompts/list nextCursor round-trips");
            Assert(raw["prompts"].IsArray, "prompts/list emits a prompts array");

            var parsed = new ListPromptsResult(raw);
            Assert(parsed.Prompts.Length == 1, "the prompt round-trips in the listing");

            var p = parsed.Prompts[0];
            AssertEqual("code_review", p.Name, "prompt name round-trips");
            AssertEqual("Code Review", p.Title, "prompt title round-trips");
            AssertEqual("Review a pull request", p.Description, "prompt description round-trips");
            AssertEqual("dev", p.Meta?["category"]?.AsString(), "prompt _meta round-trips");
            Assert(p.Icons?.Length == 1, "prompt icons round-trip");

            Assert(p.Arguments?.Length == 2, "prompt arguments round-trip");
            AssertEqual("pr_url", p.Arguments?[0].Name, "argument name round-trips");
            AssertEqual("PR URL", p.Arguments?[0].Title, "argument title round-trips");
            Assert(p.Arguments?[0].Required == true, "a required argument round-trips as required");
            Assert(p.Arguments?[1].Required == null, "an unmarked argument omits 'required'");

            return Task.CompletedTask;
        }

        private Task GetPromptResultRoundTrips()
        {
            var result = new GetPromptResult(
                new[]
                {
                    new PromptMessage("user", new TextContent("Summarise this PR")),
                    new PromptMessage("assistant", new TextContent("Sure!"))
                    {
                        Meta = new Meta(Json.Object(w => w.Write("draft", true))),
                    },
                },
                description: "A code-review prompt");

            var raw = Json.Object(result.WriteMembers);
            Assert(raw["messages"].IsArray, "prompts/get emits a messages array");
            // A prompt message carries a single content object, not an array (unlike a sampling message).
            Assert(!raw["messages"].AsObjectArray()[0]["content"].IsArray, "a prompt message carries a single content block");

            var parsed = new GetPromptResult(raw);
            AssertEqual("A code-review prompt", parsed.Description, "prompt result description round-trips");
            Assert(parsed.Messages.Length == 2, "both prompt messages round-trip");
            AssertEqual("user", parsed.Messages[0].Role, "message role round-trips");
            AssertEqual("Summarise this PR", (parsed.Messages[0].Content as TextContent)?.Text, "message content round-trips");
            Assert(parsed.Messages[1].Meta?["draft"]?.AsBool() == true, "message _meta round-trips");

            return Task.CompletedTask;
        }

        private Task GetPromptRequestReadsArguments()
        {
            var request = new GetPromptRequest("code_review",
                Json.Object(w => w.Write("pr_url", "https://github.com/x/y/pull/1")));

            var raw = Json.Object(request.WriteMembers);
            var parsed = new GetPromptRequest(raw);

            AssertEqual("code_review", parsed.Name, "prompts/get name round-trips");
            AssertEqual("https://github.com/x/y/pull/1", parsed.Arguments?["pr_url"]?.AsString(),
                "prompts/get arguments round-trip");

            // A name-only request omits arguments entirely.
            var bare = Json.Object(new GetPromptRequest("ping").WriteMembers);
            Assert(bare["arguments"] == null, "a name-only prompts/get omits arguments");

            return Task.CompletedTask;
        }

        // -- End-to-end through a real server ------------------------------------------------

        private async Task ListPromptsThroughServer()
        {
            var controller = new TestPromptController(listChangedSupported: true)
            {
                PromptsToReturn = new[]
                {
                    new Prompt("code_review", "Review a pull request") { Title = "Code Review" },
                    new Prompt("summarise", "Summarise text"),
                },
            };
            var (clientEnd, _) = await StartPromptServer(controller);

            var init = new InitializeRequest(ProtocolVersion.Latest, new ClientCapabilitiesModel(), new ClientInfo("C", "1.0.0"));
            var initResp = await clientEnd.SendRequest("initialize", init.WriteMembers);
            var promptsCap = initResp.Result?["capabilities"]?.AsObject()?["prompts"]?.AsObject();
            Assert(promptsCap?["listChanged"]?.AsBool() == true, "server advertises prompts.listChanged");

            var resp = await clientEnd.SendRequest("prompts/list", new ListPromptsRequest().WriteMembers);
            Assert(resp.IsOk, "prompts/list returns a result");

            var result = new ListPromptsResult(resp.Result);
            Assert(result.Prompts.Length == 2, "both prompts round-trip through prompts/list");
            Assert(result.Prompts.Any(p => p.Name == "code_review"), "the first prompt name round-trips");
            Assert(result.Prompts.Any(p => p.Title == "Code Review"), "the prompt title round-trips through the server");
        }

        private async Task GetPromptThroughServer()
        {
            var controller = new TestPromptController(listChangedSupported: false)
            {
                PromptResult = new GetPromptResult(
                    new[] { new PromptMessage("user", new TextContent("Review https://github.com/x/y/pull/1")) },
                    description: "A code-review prompt"),
            };
            var (clientEnd, _) = await StartPromptServer(controller);

            var request = new GetPromptRequest("code_review",
                Json.Object(w => w.Write("pr_url", "https://github.com/x/y/pull/1")));
            var resp = await clientEnd.SendRequest("prompts/get", request.WriteMembers);
            Assert(resp.IsOk, "prompts/get returns a result");

            var result = new GetPromptResult(resp.Result);
            AssertEqual("A code-review prompt", result.Description, "prompts/get description round-trips through the server");
            Assert(result.Messages.Length == 1, "prompts/get message round-trips through the server");
            AssertEqual("code_review", controller.LastGet?.Name, "the server delivered the prompt name to the controller");
            AssertEqual("https://github.com/x/y/pull/1", controller.LastGet?.Arguments?["pr_url"]?.AsString(),
                "the server delivered the prompt arguments to the controller");
        }

        private async Task PromptsListChangedNotification()
        {
            var controller = new TestPromptController(listChangedSupported: true);
            var (clientEnd, _) = await StartPromptServer(controller);

            var init = new InitializeRequest(ProtocolVersion.Latest, new ClientCapabilitiesModel(), new ClientInfo("C", "1.0.0"));
            await clientEnd.SendRequest("initialize", init.WriteMembers);

            controller.RaiseListChanged();
            var got = await WaitUntil(() => Snapshot(clientEnd.Received).Any(m =>
                Json.Parse(m)["method"]?.AsString() == "notifications/prompts/list_changed"));
            Assert(got, "notifications/prompts/list_changed reaches the client");
        }

        // -- Helpers -------------------------------------------------------------------------

        private async Task<(InMemoryTransport clientEnd, InMemoryTransport serverEnd)> StartPromptServer(
            IPromptController controller)
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var builder = new ServerBuilder("Conf Server", "1.0.0");
            builder.Context.AddNewtonsoftJson();
            builder.Context.AddInMemoryServerTransport(serverEnd);
            builder.Context.AddPromptsCapability(controller);
            var server = builder.Build();
            await server.Start();
            await clientEnd.Start();
            return (clientEnd, serverEnd);
        }

        /// <summary>A prompt controller whose list/get results are configurable, recording the last get.</summary>
        private sealed class TestPromptController : IPromptController
        {
            private readonly bool _listChangedSupported;

            public TestPromptController(bool listChangedSupported)
            {
                _listChangedSupported = listChangedSupported;
            }

            public Prompt[] PromptsToReturn { get; set; } = Array.Empty<Prompt>();
            public GetPromptResult PromptResult { get; set; }
            public GetPromptRequest LastGet { get; private set; }

            public event Action ListChanged;
            public bool IsListChangedNotificationSupported => _listChangedSupported;

            public Task<ListPromptsResult> ListPrompts(ListPromptsRequest request, McpRequestContext context)
                => Task.FromResult(new ListPromptsResult(PromptsToReturn));

            public Task<GetPromptResult> GetPrompt(GetPromptRequest request, McpRequestContext context)
            {
                LastGet = request;
                return Task.FromResult(PromptResult);
            }

            public void RaiseListChanged() => ListChanged?.Invoke();
        }
    }
}
