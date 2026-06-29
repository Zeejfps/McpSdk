#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Protocol.Models.ClientCapabilities;
using McpSdk.Protocol.Models.ServerCapabilities;

namespace McpSdk.Server.Tests
{
    /// <summary>
    /// Resources, end to end and at the model layer: <see cref="Resource"/> / <see cref="ResourceTemplate"/>
    /// metadata (title, icons, _meta) round-trips; <c>resources/list</c>, <c>resources/read</c> and
    /// <c>resources/templates/list</c> are served through a real server; <c>subscribe</c> and
    /// <c>listChanged</c> are advertised as the independent capabilities they are; and a live subscription
    /// delivers <c>resources/updated</c> + <c>resources/list_changed</c> notifications, with
    /// <c>resources/subscribe</c> returning MethodNotFound when the server never advertised it.
    /// </summary>
    public sealed class ResourcesTests : ConformanceSuite
    {
        public ResourcesTests(TestReport report) : base(report) { }

        public override string Title => "Resources";

        public override async Task Run()
        {
            await Test("resource title + icons + _meta round-trip (omitted when absent)", ResourceMetadataRoundTrips);
            await Test("resource template title + icons + _meta round-trip", ResourceTemplateMetadataRoundTrips);
            await Test("resources subscribe + listChanged parse independently", ResourceCapabilitiesAreIndependent);
            await Test("server advertises subscribe from resource-changed, not listChanged", ServerAdvertisesSubscribeFromResourceChanged);
            await Test("resources/list round-trips resources through the server", ListResourcesThroughServer);
            await Test("resources/read is routed and returns a result", ReadResourceIsRouted);
            await Test("resources/templates/list round-trips templates through the server", ListResourceTemplatesThroughServer);
            await Test("resources subscribe/unsubscribe + updated/list_changed round-trip", ResourceSubscribeAndNotifications);
            await Test("resources/subscribe -> MethodNotFound when not advertised", ResourceSubscribeNotAdvertisedIsMethodNotFound);
        }

        // -- Model round-trips ---------------------------------------------------------------

        private Task ResourceMetadataRoundTrips()
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

        private Task ResourceTemplateMetadataRoundTrips()
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

        private Task ResourceCapabilitiesAreIndependent()
        {
            // Reader: 'subscribe' and 'listChanged' are parsed independently of each other.
            var read = new ResourcesCapabilityModel(
                Json.Object(w => { w.Write("subscribe", true); w.Write("listChanged", false); }));
            Assert(read.IsResourceChangedNotificationSupported == true, "reader parses 'subscribe' independently");
            Assert(read.IsListChangedNotificationSupported == false, "reader parses 'listChanged' independently");

            return Task.CompletedTask;
        }

        // -- End-to-end through a real server ------------------------------------------------

        private async Task ServerAdvertisesSubscribeFromResourceChanged()
        {
            // A controller that supports resource-changed (subscribe) but NOT list-changed must
            // advertise subscribe:true, listChanged:false — they are not the same flag.
            var (clientEnd, _) = await StartResourceServer(
                new TestResourcesController(resourceChanged: true, listChanged: false));

            var init = new InitializeRequest(ProtocolVersion.Latest, new ClientCapabilitiesModel(), new ClientInfo("F", "1.0.0"));
            var response = await clientEnd.SendRequest("initialize", init.WriteMembers);

            var resources = response.Result?["capabilities"]?.AsObject()["resources"]?.AsObject();
            Assert(resources != null, "the server advertises a resources capability");
            Assert(resources?["subscribe"]?.AsBool() == true, "subscribe reflects resource-changed support");
            Assert(resources?["listChanged"]?.AsBool() == false, "listChanged is independent of subscribe");
        }

        private async Task ListResourcesThroughServer()
        {
            var controller = new TestResourcesController(resourceChanged: false, listChanged: false)
            {
                ResourcesToReturn = new[]
                {
                    new Resource("file:///a.txt", "a", mimeType: "text/plain"),
                    new Resource("file:///b.txt", "b", mimeType: "text/plain"),
                },
            };
            var (clientEnd, _) = await StartResourceServer(controller);

            var resp = await clientEnd.SendRequest("resources/list", new ListResourcesRequest().WriteMembers);
            Assert(resp.IsOk, "resources/list returns a result");

            var result = new ListResourcesResult(resp.Result);
            Assert(result.Resources.Length == 2, "both resources round-trip through resources/list");
            Assert(result.Resources.Any(r => r.Uri == "file:///a.txt"), "the first resource uri round-trips");
            Assert(result.Resources.Any(r => r.Name == "b"), "the second resource name round-trips");
        }

        private async Task ReadResourceIsRouted()
        {
            var controller = new TestResourcesController(resourceChanged: false, listChanged: false);
            var (clientEnd, _) = await StartResourceServer(controller);

            var resp = await clientEnd.SendRequest("resources/read", w => w.Write("uri", "file:///a.txt"));
            Assert(resp.IsOk, "resources/read returns a (non-error) result");
            Assert(controller.ReadInvoked, "the resources controller's ReadResource was invoked");
            AssertEqual("file:///a.txt", controller.ReadUri, "the requested uri reaches the controller");

            // The contents the controller returned round-trip back to the client (text + blob).
            var result = new ReadResourceResult(resp.Result);
            Assert(result.Contents.Length == 2, "resources/read returns both content entries");

            var text = result.Contents[0] as TextResourceContents;
            Assert(text != null, "the first entry parses as text resource contents");
            AssertEqual("file:///a.txt", text?.Uri, "text content uri round-trips");
            AssertEqual("text/plain", text?.MimeType, "text content mimeType round-trips");
            AssertEqual("hello", text?.Text, "text content text round-trips");

            var blob = result.Contents[1] as BlobResourceContents;
            Assert(blob != null, "the second entry parses as blob resource contents");
            AssertEqual("file:///a.txt", blob?.Uri, "blob content uri round-trips");
            AssertEqual("application/octet-stream", blob?.MimeType, "blob content mimeType round-trips");
            AssertEqual("QUJD", blob?.Blob, "blob content data round-trips");
        }

        private async Task ListResourceTemplatesThroughServer()
        {
            var controller = new TestResourcesController(resourceChanged: false, listChanged: false)
            {
                TemplatesToReturn = new[]
                {
                    new ResourceTemplate("file:///logs/{date}.log", "daily-log", mimeType: "text/plain"),
                },
            };
            var (clientEnd, _) = await StartResourceServer(controller);

            var resp = await clientEnd.SendRequest("resources/templates/list", new ListTemplatesRequest().WriteMembers);
            Assert(resp.IsOk, "resources/templates/list returns a result");

            var result = new ListTemplatesResult(resp.Result);
            Assert(result.ResourceTemplates.Length == 1, "the template round-trips through templates/list");
            AssertEqual("file:///logs/{date}.log", result.ResourceTemplates[0].UriTemplate, "template uriTemplate round-trips");
        }

        private async Task ResourceSubscribeAndNotifications()
        {
            var controller = new TestResourcesController(resourceChanged: true, listChanged: true);
            var (clientEnd, _) = await StartResourceServer(controller);

            var init = new InitializeRequest(ProtocolVersion.Latest, new ClientCapabilitiesModel(), new ClientInfo("C", "1.0.0"));
            var initResp = await clientEnd.SendRequest("initialize", init.WriteMembers);

            var resourcesCap = initResp.Result?["capabilities"]?.AsObject()?["resources"]?.AsObject();
            Assert(resourcesCap?["subscribe"]?.AsBool() == true, "server advertises resources.subscribe");

            var subResp = await clientEnd.SendRequest("resources/subscribe", w => w.Write("uri", "file://x"));
            Assert(subResp.IsOk, "resources/subscribe returns a result");
            Assert(controller.HasSubscription("file://x"), "controller recorded the subscription");

            controller.RaiseUpdated("file://x");
            var gotUpdated = await WaitUntil(() => Snapshot(clientEnd.Received).Any(m =>
            {
                var o = Json.Parse(m);
                return o["method"]?.AsString() == "notifications/resources/updated"
                    && o["params"]?.AsObject()?["uri"]?.AsString() == "file://x";
            }));
            Assert(gotUpdated, "notifications/resources/updated reaches the client carrying the uri");

            controller.RaiseListChanged();
            var gotListChanged = await WaitUntil(() => Snapshot(clientEnd.Received).Any(m =>
                Json.Parse(m)["method"]?.AsString() == "notifications/resources/list_changed"));
            Assert(gotListChanged, "notifications/resources/list_changed reaches the client");

            var unsubResp = await clientEnd.SendRequest("resources/unsubscribe", w => w.Write("uri", "file://x"));
            Assert(unsubResp.IsOk, "resources/unsubscribe returns a result");
            Assert(!controller.HasSubscription("file://x"), "controller removed the subscription");
        }

        private async Task ResourceSubscribeNotAdvertisedIsMethodNotFound()
        {
            var controller = new TestResourcesController(resourceChanged: false, listChanged: true);
            var (clientEnd, _) = await StartResourceServer(controller);

            var resp = await clientEnd.SendRequest("resources/subscribe", w => w.Write("uri", "file://x"));
            Assert(resp.IsError && resp.Error?.Code == ErrorCode.MethodNotFound,
                "resources/subscribe -> MethodNotFound when subscribe is not advertised");
        }

        // -- Helpers -------------------------------------------------------------------------

        /// <summary>
        /// Builds and starts a server exposing the given resources controller over a fresh loopback pair,
        /// and starts the raw client end. Does not perform the initialize handshake — the server serves
        /// resource methods regardless — so a test can capture the initialize response itself when it needs
        /// to assert on advertised capabilities.
        /// </summary>
        private async Task<(InMemoryTransport clientEnd, InMemoryTransport serverEnd)> StartResourceServer(
            IResourcesController controller)
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var builder = new ServerBuilder("Conf Server", "1.0.0");
            builder.Context.AddNewtonsoftJson();
            builder.Context.AddInMemoryServerTransport(serverEnd);
            builder.Context.AddResourcesCapability(controller);
            var server = builder.Build();
            await server.Start();
            await clientEnd.Start();
            return (clientEnd, serverEnd);
        }

        /// <summary>
        /// A resources controller whose list/read results and notification support are configurable, and
        /// which records subscriptions so a test can assert on them.
        /// </summary>
        private sealed class TestResourcesController : IResourcesController
        {
            private readonly bool? _resourceChanged;
            private readonly bool? _listChanged;

            public TestResourcesController(bool? resourceChanged, bool? listChanged)
            {
                _resourceChanged = resourceChanged;
                _listChanged = listChanged;
            }

            public Resource[] ResourcesToReturn { get; set; } = Array.Empty<Resource>();
            public ResourceTemplate[] TemplatesToReturn { get; set; } = Array.Empty<ResourceTemplate>();
            public bool ReadInvoked { get; private set; }
            public string ReadUri { get; private set; }
            public List<string> Subscribed { get; } = new();

            public event Action ListChanged;
            public event Action<string> ResourceUpdated;

            public bool? IsResourceChangedNotificationSupported => _resourceChanged;
            public bool? IsListChangedNotificationSupported => _listChanged;

            public Task<ListTemplatesResult> ListTemplates(ListTemplatesRequest request, McpRequestContext context)
                => Task.FromResult(new ListTemplatesResult(TemplatesToReturn));

            public Task<ListResourcesResult> ListResources(ListResourcesRequest request, McpRequestContext context)
                => Task.FromResult(new ListResourcesResult(ResourcesToReturn));

            public Task<ReadResourceResult> ReadResource(ReadResourceRequest readResourceRequest, McpRequestContext context)
            {
                ReadInvoked = true;
                ReadUri = readResourceRequest.Uri;
                // Echo the requested uri back through both contents shapes so a test can assert that the
                // uri reached the controller and that text + blob contents round-trip to the client.
                var contents = new ResourceContents[]
                {
                    new TextResourceContents(readResourceRequest.Uri, "text/plain", "hello"),
                    new BlobResourceContents(readResourceRequest.Uri, "application/octet-stream", "QUJD"),
                };
                return Task.FromResult(new ReadResourceResult(contents));
            }

            public Task Subscribe(string uri, McpRequestContext context) { lock (Subscribed) Subscribed.Add(uri); return Task.CompletedTask; }
            public Task Unsubscribe(string uri, McpRequestContext context) { lock (Subscribed) Subscribed.Remove(uri); return Task.CompletedTask; }

            public void RaiseUpdated(string uri) => ResourceUpdated?.Invoke(uri);
            public void RaiseListChanged() => ListChanged?.Invoke();
            public bool HasSubscription(string uri) { lock (Subscribed) return Subscribed.Contains(uri); }
        }
    }
}
