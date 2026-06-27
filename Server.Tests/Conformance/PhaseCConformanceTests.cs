#nullable disable
using System.Linq;
using System.Threading.Tasks;
using McpSdk.Client;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// Phase C conformance: modern tools. Verifies that tool metadata (title, behavioural
    /// annotations, icons) survives a <c>tools/list</c> round-trip, that the JSON Schema 2020-12
    /// dialect and an <c>outputSchema</c> are advertised, that a tool declaring an output schema
    /// returns both <c>structuredContent</c> and a back-compat serialized text block, that
    /// schema-validation failures come back as tool errors (SEP-1303) rather than protocol errors,
    /// and that the new audio / resource_link content types round-trip through <c>Content.Create</c>.
    /// </summary>
    public static partial class ConformanceTests
    {
        // -- tools/list metadata -------------------------------------------------------------

        private static async Task ToolMetadataInListing()
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

        private static Task SchemaDialectAndOutputSchemaEmitted()
        {
            var tool = new StructuredTool(Json).Info;
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

        // -- structured output ---------------------------------------------------------------

        private static async Task StructuredOutputRoundTrip()
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

        // -- validation errors as tool errors (SEP-1303) -------------------------------------

        private static async Task ValidationErrorIsToolError()
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

        // -- new content types (pure round-trip) ---------------------------------------------

        private static Task ContentTypesRoundTrip()
        {
            var audioJson = Json.Object(new AudioContent("audio/wav", "QUJD").WriteMembers);
            var audio = Content.Create(audioJson) as AudioContent;
            Assert(audio != null, "audio content type is recognized by Content.Create");
            AssertEqual("audio/wav", audio?.MimeType, "audio mimeType round-trips");
            AssertEqual("QUJD", audio?.Base64EncodedData, "audio data round-trips");

            var linkJson = Json.Object(
                new ResourceLinkContent("file:///x.txt", "x.txt", "X File", "a file", "text/plain").WriteMembers);
            var link = Content.Create(linkJson) as ResourceLinkContent;
            Assert(link != null, "resource_link content type is recognized by Content.Create");
            AssertEqual("file:///x.txt", link?.Uri, "resource_link uri round-trips");
            AssertEqual("x.txt", link?.Name, "resource_link name round-trips");
            AssertEqual("X File", link?.Title, "resource_link title round-trips");
            AssertEqual("text/plain", link?.MimeType, "resource_link mimeType round-trips");

            return Task.CompletedTask;
        }

        // -- validator parity across JSON adapters -------------------------------------------

        private static Task SchemaValidationAdapterParity()
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

        // -- helper --------------------------------------------------------------------------

        private static IClient ConnectClient(InMemoryTransport clientEnd)
        {
            return new ClientBuilder()
                .WithName("Phase C Client")
                .WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(clientEnd))
                .Build();
        }
    }
}
