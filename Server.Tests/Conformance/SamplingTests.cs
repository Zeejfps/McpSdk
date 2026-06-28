#nullable disable
using System;
using System.Threading.Tasks;
using McpSdk.Client;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// Tool-enabled sampling (server→client <c>sampling/createMessage</c>, 2025-11-25): a tool-capable
    /// client advertises <c>sampling.tools</c>, a sampling request carries <c>tools</c> + <c>toolChoice</c>,
    /// the result returns <c>tool_use</c> content, and <c>tool_result</c> content round-trips inside a
    /// sampling message.
    /// </summary>
    public sealed class SamplingTests : ConformanceSuite
    {
        public SamplingTests(TestReport report) : base(report) { }

        public override string Title => "Sampling (with tools)";

        public override async Task Run()
        {
            await Test("tool-capable client declares sampling.tools", SamplingToolsCapabilityDeclared);
            await Test("sampling request carries tools + toolChoice", SamplingRequestWithToolsRoundTrip);
            await Test("sampling result returns tool_use content", SamplingResultToolUseRoundTrip);
            await Test("sampling tool_result content round-trips", SamplingToolResultContentRoundTrip);
        }

        private async Task SamplingToolsCapabilityDeclared()
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

        private Task SamplingRequestWithToolsRoundTrip()
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

        private async Task SamplingResultToolUseRoundTrip()
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

        private Task SamplingToolResultContentRoundTrip()
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
