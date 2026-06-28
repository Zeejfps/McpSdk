#nullable disable
using System.Linq;
using System.Threading.Tasks;
using McpSdk.Client;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Protocol.Models.ClientCapabilities;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// Modern tools: tool metadata (title, behavioural annotations, icons) survives a <c>tools/list</c>
    /// round-trip; the JSON Schema 2020-12 dialect and an <c>outputSchema</c> are advertised; a tool with
    /// an output schema returns both <c>structuredContent</c> and a back-compat text block;
    /// schema-validation failures come back as tool errors (SEP-1303) rather than protocol errors and
    /// agree across both JSON adapters; and a controller that supports it emits
    /// <c>notifications/tools/list_changed</c>.
    /// </summary>
    public sealed class ToolsTests : ConformanceSuite
    {
        public ToolsTests(TestReport report) : base(report) { }

        public override string Title => "Tools (listing, schemas, structured output)";

        public override async Task Run()
        {
            await Test("tools/list carries title + annotations + icons", ToolMetadataInListing);
            await Test("inputSchema/outputSchema declare the 2020-12 dialect", SchemaDialectAndOutputSchemaEmitted);
            await Test("structured output: structuredContent + back-compat text", StructuredOutputRoundTrip);
            await Test("schema-validation failure returns a tool error, not a protocol error", ValidationErrorIsToolError);
            await Test("schema validation agrees across both JSON adapters", SchemaValidationAdapterParity);
            await Test("notifications/tools/list_changed reaches the client", ToolsListChangedNotification);
        }

        private async Task ToolMetadataInListing()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildServer(serverEnd);
            await server.Start();

            var client = ConnectClient(clientEnd);
            await client.Connect();

            var tools = await client.ListTools();
            var forecast = tools.Tools.FirstOrDefault(t => t.Name == "get-forecast");

            Assert(forecast != null, "get-forecast is listed");
            AssertEqual("Weather Forecast", forecast?.Title, "tool title round-trips");

            Assert(forecast?.Annotations != null, "tool annotations round-trip");
            Assert(forecast?.Annotations?.ReadOnlyHint == true, "readOnlyHint round-trips as true");
            Assert(forecast?.Annotations?.OpenWorldHint == true, "openWorldHint round-trips as true");

            Assert(forecast?.Icons != null && forecast.Icons.Length == 1, "one icon round-trips");
            AssertEqual("https://example.com/forecast.png", forecast?.Icons?[0].Src, "icon src round-trips");
            AssertEqual("image/png", forecast?.Icons?[0].MimeType, "icon mimeType round-trips");
            AssertEqual("48x48", forecast?.Icons?[0].Sizes, "icon sizes round-trips");
        }

        private Task SchemaDialectAndOutputSchemaEmitted()
        {
            var tool = new StructuredToolHandler(Json).Tool;
            var raw = Json.Object(tool.WriteMembers);

            var inputSchema = raw["inputSchema"]?.AsObject();
            Assert(inputSchema != null, "inputSchema is emitted");
            AssertEqual(JsonSchema.Dialect2020_12, inputSchema?["$schema"]?.AsString(),
                "input schema declares the 2020-12 dialect");

            var outputSchema = raw["outputSchema"]?.AsObject();
            Assert(outputSchema != null, "outputSchema is advertised");
            AssertEqual(JsonSchema.Dialect2020_12, outputSchema?["$schema"]?.AsString(),
                "output schema declares the 2020-12 dialect");

            return Task.CompletedTask;
        }

        private async Task StructuredOutputRoundTrip()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildServer(serverEnd);
            await server.Start();

            var client = ConnectClient(clientEnd);
            await client.Connect();

            var args = Json.Object(w =>
            {
                w.Write("a", 2.0);
                w.Write("b", 3.0);
            });
            var result = await client.CallTool(new CallToolRequest("add", args));

            Assert(result.IsError != true, "structured tool call did not error");
            Assert(result.StructuredContent != null, "structuredContent is present");
            Assert(result.StructuredContent?["sum"]?.AsDouble() == 5.0, "structuredContent carries the computed sum");

            // Back-compat: the same payload is mirrored as a serialized-JSON text block.
            var text = result.Content.OfType<TextContent>().FirstOrDefault()?.Text;
            Assert(text != null && text.Contains("sum") && text.Contains("5"),
                $"structured result includes a back-compat text block (got '{text}')");
        }

        private async Task ValidationErrorIsToolError()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildServer(serverEnd);
            await server.Start();

            var client = ConnectClient(clientEnd);
            await client.Connect();

            // 'b' is required by the schema but omitted. Reaching the asserts (no thrown
            // TransportErrorException) is itself proof this came back as an OK response.
            var args = Json.Object(w => w.Write("a", 2.0));
            var result = await client.CallTool(new CallToolRequest("add", args));

            Assert(result.IsError == true, "schema-invalid args return a tool error (isError: true)");
            Assert(result.Content.OfType<TextContent>().Any(), "the tool error carries a textual explanation");
        }

        private Task SchemaValidationAdapterParity()
        {
            var newtonsoft = new McpSdk.Adapter.Newtonsoft.Json.NewtonsoftJson();
            var systemText = new McpSdk.Adapter.System.Text.Json.SystemJson();

            // A 2020-12 object schema with two required numeric properties.
            var schema = new ObjectSchema
            {
                { "a", new NumberSchema() },
                { "b", new NumberSchema() },
            };

            foreach (var (name, json) in new (string, IJson)[] { ("Newtonsoft", newtonsoft), ("SystemText", systemText) })
            {
                var schemaObj = schema.AsJsonObject(json);

                var good = json.Object(w => { w.Write("a", 1.0); w.Write("b", 2.0); });
                var goodValid = good.IsValid(schemaObj, out _);
                Assert(goodValid, $"{name}: valid args pass 2020-12 validation");

                // 'b' is required but omitted — every adapter must catch this, not silently pass.
                var bad = json.Object(w => w.Write("a", 1.0));
                var badValid = bad.IsValid(schemaObj, out var errors);
                Assert(!badValid, $"{name}: missing required field fails validation");
                Assert(!badValid && errors != null && errors.Count > 0, $"{name}: failure reports at least one error");
            }

            return Task.CompletedTask;
        }

        private async Task ToolsListChangedNotification()
        {
            // DefaultToolsController never advertises list_changed, so a controller that does is used to
            // drive the notifications/tools/list_changed path the server wires on Start.
            var controller = new ListChangingToolsController();
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = new ServerBuilder()
                .WithName("Conf Server").WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(serverEnd))
                .WithToolsCapability(controller)
                .Build();
            await server.Start();
            await clientEnd.Start();

            var init = new InitializeRequest(ProtocolVersion.Latest, new ClientCapabilitiesModel(), new ClientInfo("C", "1.0.0"));
            var initResp = await clientEnd.SendRequest("initialize", init.WriteMembers);
            var toolsCap = initResp.Result?["capabilities"]?.AsObject()?["tools"]?.AsObject();
            Assert(toolsCap?["listChanged"]?.AsBool() == true, "server advertises tools.listChanged");

            controller.RaiseListChanged();
            var got = await WaitUntil(() => Snapshot(clientEnd.Received).Any(m =>
                Json.Parse(m)["method"]?.AsString() == "notifications/tools/list_changed"));
            Assert(got, "notifications/tools/list_changed reaches the client");
        }

        /// <summary>A minimal tools controller that advertises list_changed and can raise it on demand.</summary>
        private sealed class ListChangingToolsController : IToolsController
        {
            public event System.Action ListChanged;
            public bool IsListChangedNotificationSupported => true;

            public Task<ListToolsResult> ListTools(ListToolsRequest request, McpRequestContext context)
                => Task.FromResult(new ListToolsResult(new[] { new Tool("noop", "no-op", new ObjectSchema()) }));

            public Task<CallToolResult> CallTool(CallToolRequest request, McpRequestContext context)
                => Task.FromResult(new CallToolResult(new Content[] { new TextContent("ok") }, false));

            public void RaiseListChanged() => ListChanged?.Invoke();
        }
    }
}
