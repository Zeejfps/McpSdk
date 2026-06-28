#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McpSdk.Client;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// Phase E conformance: elicitation + richer sampling (2025-11-25). Verifies that a client
    /// advertises the <c>elicitation</c> and <c>sampling.tools</c> capabilities it supports, that
    /// server→client <c>elicitation/create</c> requests round-trip through the three-action model in
    /// both form and URL modes (and that an undeclared mode is rejected), that the restricted
    /// <c>requestedSchema</c> — including all four enum shapes and primitive defaults — survives a
    /// round-trip, and that tool-enabled <c>sampling/createMessage</c> requests carry
    /// <c>tools</c>/<c>toolChoice</c> and return <c>tool_use</c> / <c>tool_result</c> content.
    /// </summary>
    public static partial class ConformanceTests
    {
        // -- capability declaration ----------------------------------------------------------

        private static async Task ElicitationCapabilityDeclared()
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

        private static async Task ElicitationFormOnlyOmitsUrl()
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

        // -- form mode round-trip ------------------------------------------------------------

        private static async Task ElicitationFormAccept()
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

        private static async Task ElicitationDeclineAndCancel()
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

        // -- URL mode ------------------------------------------------------------------------

        private static async Task ElicitationUrlMode()
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

        private static async Task ElicitationUnsupportedModeRejected()
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

        private static async Task ElicitationWithoutControllerErrors()
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

        // -- restricted schema (enum forms + defaults) ---------------------------------------

        private static Task EnumSchemaAllFormsRoundTrip()
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

        // -- sampling with tools -------------------------------------------------------------

        private static async Task SamplingToolsCapabilityDeclared()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            ActAsRawServer(serverEnd, ProtocolVersion.Latest);
            await serverEnd.Start();

            var controller = new TestSamplingController(supportsTools: true,
                _ => new CreateMessagesResult("assistant", "m", new TextContent("hi"), "endTurn"));
            var client = ConnectClientWith(clientEnd, sampling: new TestSamplingFactory(controller));
            await client.Connect();

            var sampling = FindInitializeCapabilities(clientEnd.Sent)?["sampling"]?.AsObject();
            Assert(sampling != null, "client advertises the sampling capability");
            Assert(sampling?["tools"] != null, "a tool-capable client declares sampling.tools");
        }

        private static Task SamplingRequestWithToolsRoundTrip()
        {
            var tool = new Tool("get_weather", "Get current weather for a city",
                new ObjectSchema { { "city", new StringSchema() } });
            var message = new SamplingMessage("user", new TextContent("What's the weather in Paris?"));

            var request = new CreateMessageRequest(
                new[] { message }, maxTokens: 1000, tools: new[] { tool }, toolChoice: ToolChoice.Auto);

            var raw = Json.Object(request.WriteMembers);
            var parsed = new CreateMessageRequest(raw);

            Assert(parsed.Tools != null && parsed.Tools.Length == 1, "tools round-trip on the request");
            AssertEqual("get_weather", parsed.Tools?[0].Name, "tool name round-trips");
            Assert(parsed.Tools?[0].InputSchema != null, "tool inputSchema round-trips");
            Assert(parsed.ToolChoice != null, "toolChoice round-trips");
            AssertEqual(ToolChoice.ModeAuto, parsed.ToolChoice?.Mode, "toolChoice mode round-trips");
            Assert(parsed.MaxTokens == 1000, "maxTokens round-trips alongside tools");
            Assert(parsed.Messages.Length == 1 && parsed.Messages[0].Content.Length == 1
                   && parsed.Messages[0].Content[0] is TextContent, "a plain message keeps a single text block");

            return Task.CompletedTask;
        }

        private static async Task SamplingResultToolUseRoundTrip()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            ActAsRawServer(serverEnd, ProtocolVersion.Latest);
            await serverEnd.Start();

            var controller = new TestSamplingController(supportsTools: true, request =>
            {
                // When tools are offered, answer with a tool_use turn.
                if (request.Tools is { Length: > 0 })
                {
                    var toolUse = new ToolUseContent("call_abc123", "get_weather",
                        Json.Object(w => w.Write("city", "Paris")));
                    return new CreateMessagesResult("assistant", "test-model", new Content[] { toolUse }, "toolUse");
                }

                return new CreateMessagesResult("assistant", "test-model", new TextContent("no tools"), "endTurn");
            });
            var client = ConnectClientWith(clientEnd, sampling: new TestSamplingFactory(controller));
            await client.Connect();

            var tool = new Tool("get_weather", "Get current weather for a city",
                new ObjectSchema { { "city", new StringSchema() } });
            var request = new CreateMessageRequest(
                new[] { new SamplingMessage("user", new TextContent("Weather in Paris?")) },
                maxTokens: 1000, tools: new[] { tool }, toolChoice: ToolChoice.Auto);

            var response = await serverEnd.SendRequest("sampling/createMessage", request.WriteMembers);
            Assert(response.IsOk, "the tool-enabled sampling request succeeded");

            var result = new CreateMessagesResult(response.Result);
            AssertEqual("toolUse", result.StopReason, "the result reports a toolUse stop reason");
            AssertEqual("test-model", result.Model, "the result model round-trips");
            Assert(result.Content.Length == 1, "the result carries one content block");

            var toolUseContent = result.Content[0] as ToolUseContent;
            Assert(toolUseContent != null, "the content block is tool_use content");
            AssertEqual("call_abc123", toolUseContent?.Id, "tool_use id round-trips");
            AssertEqual("get_weather", toolUseContent?.Name, "tool_use name round-trips");
            AssertEqual("Paris", toolUseContent?.Input?["city"]?.AsString(), "tool_use input round-trips");

            // The client parsed the tools + toolChoice the server offered.
            Assert(controller.LastRequest?.Tools?.Length == 1, "the client received the offered tools");
            AssertEqual(ToolChoice.ModeAuto, controller.LastRequest?.ToolChoice?.Mode, "the client received the toolChoice");
        }

        private static Task SamplingToolResultContentRoundTrip()
        {
            var message = new SamplingMessage("user",
                new ToolResultContent("call_abc123", new TextContent("Weather in Paris: 18C, partly cloudy")),
                new ToolResultContent("call_def456", new TextContent("Weather in London: 15C, rainy")));

            var raw = Json.Object(message.WriteMembers);
            Assert(raw["content"].IsArray, "a multi-block message serialises content as an array");

            var parsed = new SamplingMessage(raw);
            Assert(parsed.Content.Length == 2, "both tool_result blocks round-trip");

            var first = parsed.Content[0] as ToolResultContent;
            Assert(first != null, "the first block parses as tool_result content");
            AssertEqual("call_abc123", first?.ToolUseId, "tool_result toolUseId round-trips");
            Assert(first?.Content.Length == 1 && first.Content[0] is TextContent, "tool_result nested content round-trips");
            AssertEqual("Weather in Paris: 18C, partly cloudy",
                (first?.Content[0] as TextContent)?.Text, "tool_result nested text round-trips");

            return Task.CompletedTask;
        }

        private static Task ContentSingleOrArrayParsing()
        {
            var single = Json.Object(w => w.Write("content", new TextContent("hi")));
            var one = single["content"].AsArrayOrSingle(Content.FromJsonObject);
            Assert(one.Length == 1 && one[0] is TextContent, "a single content object parses to one block");

            var array = Json.Object(w => w.Write("content", new Content[] { new TextContent("a"), new TextContent("b") }));
            var many = array["content"].AsArrayOrSingle(Content.FromJsonObject);
            Assert(many.Length == 2, "a content array parses to many blocks");

            var none = Json.Object(w => w.Write("role", "user"));
            var empty = none["content"].AsArrayOrSingle(Content.FromJsonObject);
            Assert(empty.Length == 0, "an absent content property parses to an empty array");

            return Task.CompletedTask;
        }

        // -- helpers -------------------------------------------------------------------------

        private static IClient ConnectClientWith(
            InMemoryTransport clientEnd,
            IElicitationCapabilityFactory elicitation = null,
            ISamplingCapabilityFactory sampling = null)
        {
            var builder = new ClientBuilder()
                .WithName("Phase E Client")
                .WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(clientEnd));

            if (elicitation != null)
                builder.WithElicitationCapability(elicitation);
            if (sampling != null)
                builder.WithSamplingCapability(sampling);

            return builder.Build();
        }

        /// <summary>Pulls the <c>capabilities</c> object out of the client's sent <c>initialize</c> request.</summary>
        private static IJsonObject FindInitializeCapabilities(List<string> sent)
        {
            foreach (var message in Snapshot(sent))
            {
                var parsed = Json.Parse(message);
                if (parsed["method"]?.AsString() == "initialize")
                    return parsed["params"]?.AsObject()["capabilities"]?.AsObject();
            }
            return null;
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

        private sealed class TestSamplingController : ISamplingController
        {
            private readonly Func<CreateMessageRequest, CreateMessagesResult> _respond;

            public TestSamplingController(bool supportsTools, Func<CreateMessageRequest, CreateMessagesResult> respond)
            {
                SupportsTools = supportsTools;
                _respond = respond;
            }

            public bool SupportsTools { get; }
            public CreateMessageRequest LastRequest { get; private set; }

            public Task<CreateMessagesResult> CreateMessages(CreateMessageRequest request)
            {
                LastRequest = request;
                return Task.FromResult(_respond(request));
            }
        }

        private sealed class TestSamplingFactory : ISamplingCapabilityFactory
        {
            private readonly ISamplingController _controller;
            public TestSamplingFactory(ISamplingController controller) => _controller = controller;
            public ISamplingController Create() => _controller;
        }
    }
}
