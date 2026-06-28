#nullable disable
using System.Linq;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Protocol.Models.ClientCapabilities;
using McpSdk.Protocol.Models.ServerCapabilities;
using McpSdk.Shared;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// Phase F conformance: resources / prompts / completion polish (2025-11-25). Verifies that the
    /// 2025-06-18 <c>title</c> and 2025-11-25 <c>icons</c> + <c>_meta</c> round-trip on
    /// <see cref="Resource"/>, <see cref="ResourceTemplate"/>, <see cref="Prompt"/> (with its
    /// <see cref="PromptArgument"/>s) and <see cref="PromptMessage"/>; that <c>prompts/list</c>,
    /// <c>prompts/get</c> and <c>resources/templates/list</c> carry their item arrays; that a
    /// completion request carries the new <c>context</c>; and that a server advertises <c>subscribe</c>
    /// and <c>listChanged</c> as the independent resource capabilities they are.
    /// </summary>
    public static partial class ConformanceTests
    {
        // -- Resource ------------------------------------------------------------------------

        private static Task ResourceMetadataRoundTrips()
        {
            var resource = new Resource(
                "file:///readme.md", "readme",
                description: "The project readme", mimeType: "text/markdown",
                title: "Read Me",
                icons: new[] { new Icon("https://example.com/i.png", "image/png", "48x48") },
                meta: new Meta(Json.Object(w => w.Write("source", "disk"))));

            var raw = Json.Object(resource.WriteMembers);
            var parsed = new Resource(raw);

            AssertEqual("file:///readme.md", parsed.Uri, "resource uri round-trips");
            AssertEqual("readme", parsed.Name, "resource name round-trips");
            AssertEqual("Read Me", parsed.Title, "resource title round-trips");
            AssertEqual("text/markdown", parsed.MimeType, "resource mimeType round-trips");
            Assert(parsed.Icons?.Length == 1, "resource icons round-trip");
            AssertEqual("https://example.com/i.png", parsed.Icons?[0].Src, "resource icon src round-trips");
            AssertEqual("disk", parsed.Meta?["source"]?.AsString(), "resource _meta round-trips");

            // A bare resource omits the optional fields entirely (not null-valued).
            var bare = Json.Object(new Resource("file:///x", "x").WriteMembers);
            Assert(bare["title"] == null, "a bare resource omits title");
            Assert(bare["icons"] == null, "a bare resource omits icons");
            Assert(bare["_meta"] == null, "a bare resource omits _meta");

            return Task.CompletedTask;
        }

        // -- Prompt + PromptArgument ----------------------------------------------------------

        private static Task PromptListingRoundTrips()
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

        // -- PromptMessage + GetPromptResult --------------------------------------------------

        private static Task GetPromptResultRoundTrips()
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

        // -- ResourceTemplate -----------------------------------------------------------------

        private static Task ResourceTemplateListingRoundTrips()
        {
            var template = new ResourceTemplate("file:///logs/{date}.log", "daily-log",
                description: "A day's log file", mimeType: "text/plain")
            {
                Title = "Daily Log",
                Icons = new[] { new Icon("https://example.com/log.png") },
                Meta = new Meta(Json.Object(w => w.Write("rotated", true))),
            };

            var result = new ListTemplatesResult(new[] { template });
            var raw = Json.Object(result.WriteMembers);
            Assert(raw["resourceTemplates"].IsArray, "templates/list emits a resourceTemplates array");
            Assert(raw["nextCursor"] == null, "a final-page templates listing omits nextCursor");

            var parsed = new ListTemplatesResult(raw);
            Assert(parsed.ResourceTemplates.Length == 1, "the template round-trips in the listing");

            var t = parsed.ResourceTemplates[0];
            AssertEqual("file:///logs/{date}.log", t.UriTemplate, "template uriTemplate round-trips");
            AssertEqual("daily-log", t.Name, "template name round-trips");
            AssertEqual("Daily Log", t.Title, "template title round-trips");
            AssertEqual("text/plain", t.MimeType, "template mimeType round-trips");
            Assert(t.Icons?.Length == 1, "template icons round-trip");
            Assert(t.Meta?["rotated"]?.AsBool() == true, "template _meta round-trips");

            return Task.CompletedTask;
        }

        // -- prompts/get request --------------------------------------------------------------

        private static Task GetPromptRequestReadsArguments()
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

        // -- completion context ---------------------------------------------------------------

        private static Task CompletionContextRoundTrips()
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

        // -- resource capabilities (subscribe vs listChanged) ---------------------------------

        private static Task ResourceCapabilitiesAreIndependent()
        {
            // Reader: 'subscribe' and 'listChanged' are parsed independently of each other.
            var read = new ResourcesCapabilityModel(
                Json.Object(w => { w.Write("subscribe", true); w.Write("listChanged", false); }));
            Assert(read.IsResourceChangedNotificationSupported == true, "reader parses 'subscribe' independently");
            Assert(read.IsListChangedNotificationSupported == false, "reader parses 'listChanged' independently");

            return Task.CompletedTask;
        }

        private static async Task ServerAdvertisesSubscribeFromResourceChanged()
        {
            // A controller that supports resource-changed (subscribe) but NOT list-changed must
            // advertise subscribe:true, listChanged:false — they are not the same flag (McpServer.cs bug).
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = new ServerBuilder()
                .WithName("Conf Server")
                .WithVersion("1.0.0")
                .ConfigureContext(c => c
                    .AddSingleton<ITransportFactory>(new FixedTransportFactory(serverEnd))
                    .AddResourcesCapability(new TestResourcesController(resourceChanged: true, listChanged: false)))
                .Build();
            await server.Start();
            await clientEnd.Start();

            var request = new InitializeRequest(ProtocolVersion.Latest, new ClientCapabilitiesModel(), new ClientInfo("F", "1.0.0"));
            var response = await clientEnd.SendRequest("initialize", request.WriteMembers);

            var resources = response.Result?["capabilities"]?.AsObject()["resources"]?.AsObject();
            Assert(resources != null, "the server advertises a resources capability");
            Assert(resources?["subscribe"]?.AsBool() == true, "subscribe reflects resource-changed support");
            Assert(resources?["listChanged"]?.AsBool() == false, "listChanged is independent of subscribe");
        }

        // -- helpers -------------------------------------------------------------------------

        /// <summary>A minimal resources controller used only to surface its capability flags.</summary>
        private sealed class TestResourcesController : IResourcesController
        {
            public TestResourcesController(bool resourceChanged, bool listChanged)
            {
                IsResourceChangedNotificationSupported = resourceChanged;
                IsListChangedNotificationSupported = listChanged;
            }

            public event System.Action ListChanged { add { } remove { } }
            public event System.Action ResourceChanged { add { } remove { } }

            public bool? IsResourceChangedNotificationSupported { get; }
            public bool? IsListChangedNotificationSupported { get; }

            public Task<ListTemplatesResult> ListTemplates(ListTemplatesRequest request)
                => Task.FromResult(new ListTemplatesResult(System.Array.Empty<ResourceTemplate>()));

            public Task<ListResourcesResult> ListResources(ListResourcesRequest request)
                => Task.FromResult(new ListResourcesResult(System.Array.Empty<Resource>()));

            public Task<ReadResourceResult> ReadResource(ReadResourceRequest readResourceRequest)
                => Task.FromResult<ReadResourceResult>(null);
        }
    }
}
