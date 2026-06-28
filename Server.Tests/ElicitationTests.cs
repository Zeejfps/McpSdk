#nullable disable
using System;
using System.Linq;
using System.Threading.Tasks;
using McpSdk.Client;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server.Tests
{
    /// <summary>
    /// Elicitation (server→client <c>elicitation/create</c>, 2025-11-25): the client advertises the
    /// <c>elicitation</c> form/url modes it supports, the three-action model (accept / decline / cancel)
    /// round-trips in both form and URL modes, an undeclared mode is rejected with InvalidParams, a
    /// request without a controller errors with MethodNotFound, and the restricted <c>requestedSchema</c>
    /// — every enum shape plus primitive defaults — survives a round-trip.
    /// </summary>
    public sealed class ElicitationTests : ConformanceSuite
    {
        public ElicitationTests(TestReport report) : base(report) { }

        public override string Title => "Elicitation";

        public override async Task Run()
        {
            await Test("client advertises elicitation (form + url) capability", ElicitationCapabilityDeclared);
            await Test("a form-only client omits the url mode from its capability", ElicitationFormOnlyOmitsUrl);
            await Test("form-mode elicitation accept round-trips content + schema", ElicitationFormAccept);
            await Test("elicitation decline and cancel round-trip", ElicitationDeclineAndCancel);
            await Test("url-mode elicitation consent round-trips (no content)", ElicitationUrlMode);
            await Test("an undeclared elicitation mode is rejected (InvalidParams)", ElicitationUnsupportedModeRejected);
            await Test("elicitation without a controller errors (MethodNotFound)", ElicitationWithoutControllerErrors);
            await Test("requestedSchema enum forms + primitive defaults round-trip", EnumSchemaAllFormsRoundTrip);
        }

        private async Task ElicitationCapabilityDeclared()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            ActAsRawServer(serverEnd, ProtocolVersion.Latest);
            await serverEnd.Start();

            var controller = new TestElicitationController(form: true, url: true, _ => ElicitResult.Cancel());
            var client = ConnectClientWith(clientEnd, elicitation: new TestElicitationFactory(controller));
            await client.Connect();

            var capabilities = FindInitializeCapabilities(clientEnd.Sent);
            var elicitation = capabilities?["elicitation"]?.AsObject();
            Assert(elicitation != null, "client advertises the elicitation capability");
            Assert(elicitation?["form"] != null, "form mode is declared");
            Assert(elicitation?["url"] != null, "url mode is declared");
        }

        private async Task ElicitationFormOnlyOmitsUrl()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            ActAsRawServer(serverEnd, ProtocolVersion.Latest);
            await serverEnd.Start();

            var controller = new TestElicitationController(form: true, url: false, _ => ElicitResult.Cancel());
            var client = ConnectClientWith(clientEnd, elicitation: new TestElicitationFactory(controller));
            await client.Connect();

            var elicitation = FindInitializeCapabilities(clientEnd.Sent)?["elicitation"]?.AsObject();
            Assert(elicitation?["form"] != null, "a form-only client still declares form mode");
            Assert(elicitation?["url"] == null, "a form-only client does not declare url mode");
        }

        private async Task ElicitationFormAccept()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            ActAsRawServer(serverEnd, ProtocolVersion.Latest);
            await serverEnd.Start();

            var controller = new TestElicitationController(form: true, url: false,
                _ => ElicitResult.Accept(Json.Object(w => w.Write("name", "octocat"))));
            var client = ConnectClientWith(clientEnd, elicitation: new TestElicitationFactory(controller));
            await client.Connect();

            var request = new ElicitRequest(
                "Please provide your GitHub username",
                new RequestedSchema().Add("name", new StringSchema(), required: true));

            var response = await serverEnd.SendRequest("elicitation/create", request.WriteMembers);
            Assert(response.IsOk, "client returned a successful elicitation result");

            var result = new ElicitResult(response.Result);
            AssertEqual(ElicitResult.ActionAccept, result.Action, "form accept returns action 'accept'");
            AssertEqual("octocat", result.Content?["name"]?.AsString(), "accept content carries the submitted field");

            // The client parsed the restricted schema it received.
            var seen = controller.LastRequest?.RequestedSchema;
            Assert(seen != null, "the controller received a parsed requestedSchema");
            Assert(seen?["name"] is StringSchema, "the 'name' property parsed as a string schema");
            Assert(seen?.IsRequired("name") == true, "the 'name' property round-trips as required");
        }

        private async Task ElicitationDeclineAndCancel()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            ActAsRawServer(serverEnd, ProtocolVersion.Latest);
            await serverEnd.Start();

            var controller = new TestElicitationController(form: true, url: false,
                request => request.Message == "decline" ? ElicitResult.Decline() : ElicitResult.Cancel());
            var client = ConnectClientWith(clientEnd, elicitation: new TestElicitationFactory(controller));
            await client.Connect();

            var declineReq = new ElicitRequest("decline", new RequestedSchema());
            var declineResp = await serverEnd.SendRequest("elicitation/create", declineReq.WriteMembers);
            AssertEqual(ElicitResult.ActionDecline, new ElicitResult(declineResp.Result).Action, "explicit decline round-trips");

            var cancelReq = new ElicitRequest("dismiss", new RequestedSchema());
            var cancelResp = await serverEnd.SendRequest("elicitation/create", cancelReq.WriteMembers);
            AssertEqual(ElicitResult.ActionCancel, new ElicitResult(cancelResp.Result).Action, "dismissal round-trips as cancel");
        }

        private async Task ElicitationUrlMode()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            ActAsRawServer(serverEnd, ProtocolVersion.Latest);
            await serverEnd.Start();

            var controller = new TestElicitationController(form: true, url: true,
                request => request.IsUrlMode ? ElicitResult.AcceptUrl() : ElicitResult.Cancel());
            var client = ConnectClientWith(clientEnd, elicitation: new TestElicitationFactory(controller));
            await client.Connect();

            var request = ElicitRequest.ForUrl(
                "Please provide your API key to continue.",
                "https://mcp.example.com/ui/set_api_key",
                "550e8400-e29b-41d4-a716-446655440000");

            var response = await serverEnd.SendRequest("elicitation/create", request.WriteMembers);
            var result = new ElicitResult(response.Result);

            AssertEqual(ElicitResult.ActionAccept, result.Action, "url-mode consent returns 'accept'");
            Assert(result.Content == null, "a url-mode accept carries no content");

            Assert(controller.LastRequest?.IsUrlMode == true, "the client recognised url mode");
            AssertEqual("https://mcp.example.com/ui/set_api_key", controller.LastRequest?.Url, "the url round-trips");
            AssertEqual("550e8400-e29b-41d4-a716-446655440000", controller.LastRequest?.ElicitationId, "the elicitationId round-trips");
        }

        private async Task ElicitationUnsupportedModeRejected()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            ActAsRawServer(serverEnd, ProtocolVersion.Latest);
            await serverEnd.Start();

            // Client supports form mode only; a url-mode request must be rejected before reaching Elicit.
            var controller = new TestElicitationController(form: true, url: false, _ => ElicitResult.AcceptUrl());
            var client = ConnectClientWith(clientEnd, elicitation: new TestElicitationFactory(controller));
            await client.Connect();

            var request = ElicitRequest.ForUrl("secret", "https://example.com", "id-1");
            var response = await serverEnd.SendRequest("elicitation/create", request.WriteMembers);

            Assert(response.IsError, "an undeclared mode is rejected with an error");
            Assert(response.Error?.Code == ErrorCode.InvalidParams, "the rejection uses InvalidParams (-32602)");
            Assert(controller.LastRequest == null, "the controller is never invoked for an unsupported mode");
        }

        private async Task ElicitationWithoutControllerErrors()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            ActAsRawServer(serverEnd, ProtocolVersion.Latest);
            await serverEnd.Start();

            // No elicitation capability registered at all.
            var client = ConnectClientWith(clientEnd);
            await client.Connect();

            var capabilities = FindInitializeCapabilities(clientEnd.Sent);
            Assert(capabilities?["elicitation"] == null, "a client without the capability does not advertise it");

            var request = new ElicitRequest("hi", new RequestedSchema());
            var response = await serverEnd.SendRequest("elicitation/create", request.WriteMembers);
            Assert(response.IsError, "an unsupported elicitation request errors rather than hanging");
            Assert(response.Error?.Code == ErrorCode.MethodNotFound, "the rejection uses MethodNotFound (-32601)");
        }

        private Task EnumSchemaAllFormsRoundTrip()
        {
            var schema = new RequestedSchema()
                .Add("color", new EnumSchema
                {
                    Values = new[] { "Red", "Green", "Blue" },
                    Default = new[] { "Red" },
                }, required: true)
                .Add("hex", new EnumSchema
                {
                    Values = new[] { "#FF0000", "#00FF00", "#0000FF" },
                    Titles = new[] { "Red", "Green", "Blue" },
                    Default = new[] { "#FF0000" },
                }, required: false)
                .Add("colors", new EnumSchema
                {
                    MultiSelect = true,
                    Values = new[] { "Red", "Green", "Blue" },
                    MinItems = 1,
                    MaxItems = 2,
                    Default = new[] { "Red", "Green" },
                }, required: false)
                .Add("hexes", new EnumSchema
                {
                    MultiSelect = true,
                    Values = new[] { "#FF0000", "#00FF00" },
                    Titles = new[] { "Red", "Green" },
                }, required: false)
                .Add("name", new StringSchema { Default = "bob" }, required: false)
                .Add("age", new NumberSchema { IsInteger = true, Default = 21 }, required: false)
                .Add("subscribe", new BooleanSchema { Default = true }, required: false);

            var raw = Json.Object(schema.WriteMembers);
            var properties = raw["properties"].AsObject();

            // Wire-shape checks against the spec examples.
            Assert(properties["color"]?.AsObject()["enum"] != null, "untitled single-select emits an 'enum' array");
            Assert(properties["hex"]?.AsObject()["oneOf"] != null, "titled single-select emits a 'oneOf' array");
            AssertEqual("array", properties["colors"]?.AsObject()["type"]?.AsString(), "multi-select emits type 'array'");
            Assert(properties["colors"]?.AsObject()["items"]?.AsObject()["enum"] != null, "untitled multi-select items carry an 'enum'");
            Assert(properties["hexes"]?.AsObject()["items"]?.AsObject()["anyOf"] != null, "titled multi-select items carry an 'anyOf'");
            AssertEqual("integer", properties["age"]?.AsObject()["type"]?.AsString(), "integer schema emits type 'integer'");

            // Structured round-trip.
            var parsed = new RequestedSchema(raw);

            var color = parsed["color"] as EnumSchema;
            Assert(color != null && !color.MultiSelect, "color parses as a single-select enum");
            Assert(color?.Titles == null, "untitled single-select has no titles");
            Assert(color != null && color.Values.SequenceEqual(new[] { "Red", "Green", "Blue" }), "color values round-trip");
            AssertEqual("Red", color?.Default?.FirstOrDefault(), "color default round-trips");

            var hex = parsed["hex"] as EnumSchema;
            Assert(hex != null && !hex.MultiSelect, "hex parses as a single-select enum");
            Assert(hex != null && hex.Titles.SequenceEqual(new[] { "Red", "Green", "Blue" }), "titled single-select titles round-trip");
            Assert(hex != null && hex.Values.SequenceEqual(new[] { "#FF0000", "#00FF00", "#0000FF" }), "titled single-select consts round-trip");
            AssertEqual("#FF0000", hex?.Default?.FirstOrDefault(), "titled single-select default round-trips");

            var colors = parsed["colors"] as EnumSchema;
            Assert(colors != null && colors.MultiSelect, "colors parses as a multi-select enum");
            Assert(colors?.Titles == null, "untitled multi-select has no titles");
            Assert(colors?.MinItems == 1 && colors?.MaxItems == 2, "multi-select min/max items round-trip");
            Assert(colors != null && colors.Default.SequenceEqual(new[] { "Red", "Green" }), "multi-select default round-trips");

            var hexes = parsed["hexes"] as EnumSchema;
            Assert(hexes != null && hexes.MultiSelect, "hexes parses as a multi-select enum");
            Assert(hexes != null && hexes.Titles.SequenceEqual(new[] { "Red", "Green" }), "titled multi-select titles round-trip");

            Assert((parsed["name"] as StringSchema)?.Default == "bob", "string default round-trips");
            var age = parsed["age"] as NumberSchema;
            Assert(age != null && age.IsInteger && age.Default == 21, "integer default round-trips");
            Assert((parsed["subscribe"] as BooleanSchema)?.Default == true, "boolean default round-trips");

            Assert(parsed.IsRequired("color") && !parsed.IsRequired("age"), "the required set round-trips");

            return Task.CompletedTask;
        }

        private sealed class TestElicitationController : IElicitationController
        {
            private readonly Func<ElicitRequest, ElicitResult> _respond;

            public TestElicitationController(bool form, bool url, Func<ElicitRequest, ElicitResult> respond)
            {
                SupportsFormMode = form;
                SupportsUrlMode = url;
                _respond = respond;
            }

            public bool SupportsFormMode { get; }
            public bool SupportsUrlMode { get; }
            public ElicitRequest LastRequest { get; private set; }

            public Task<ElicitResult> Elicit(ElicitRequest request)
            {
                LastRequest = request;
                return Task.FromResult(_respond(request));
            }
        }

        private sealed class TestElicitationFactory : IElicitationCapabilityFactory
        {
            private readonly IElicitationController _controller;
            public TestElicitationFactory(IElicitationController controller) => _controller = controller;
            public IElicitationController Create() => _controller;
        }
    }
}
