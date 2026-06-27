#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Client;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Protocol.Models.ClientCapabilities;
using McpSdk.Protocol.Models.ServerCapabilities;
using McpSdk.Shared;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// Phase A conformance suite: protocol-version negotiation (modern peer, legacy peer,
    /// unsupported peer) and JSON-RPC request-id correctness (numeric + string ids), driven over an
    /// in-process loopback transport so no network or external server is required.
    ///
    /// Run with: <c>dotnet run --project Server.Tests -- conformance</c>
    /// </summary>
    public static partial class ConformanceTests
    {
        private static readonly IJson Json = new NewtonsoftJson();
        private static readonly ILoggerFactory Loggers = new NullLoggerFactory();

        private static int _passed;
        private static int _failed;

        public static async Task<int> RunAll()
        {
            Console.WriteLine("=== Phase A Conformance ===");

            await RunTest("Modern peer full handshake (real client <-> real server)", ModernPeerHandshake);
            await RunTest("Legacy client (2024-11-05) -> modern server echoes legacy version", LegacyClientNegotiation);
            await RunTest("Unsupported client version -> server offers Latest, never errors", UnsupportedClientNegotiation);
            await RunTest("Modern client -> legacy server (2024-11-05) connects", ModernClientLegacyServer);
            await RunTest("Modern client -> unsupported server disconnects cleanly", ModernClientUnsupportedServer);
            await RunTest("String request id is echoed back as a string", StringRequestId);
            await RunTest("InitializeResult parses capabilities + serverInfo", InitializeResultParsing);

            Console.WriteLine();
            Console.WriteLine("=== Phase B Conformance (stdio) ===");

            await RunTest("Framing collapses embedded CR/LF/TAB to a single line", FramingStripsEmbeddedControl);
            await RunTest("Framing preserves escaped newlines inside string values", FramingPreservesEscapedNewlines);
            await RunTest("Batch detection flags arrays, not objects", BatchDetection);
            await RunTest("Server ignores an incoming JSON-RPC batch (no response)", BatchRejectionRoundTrip);
            await RunTest("Real stdio round-trip: initialize + tools/list + tools/call", StdioRoundTrip);

            Console.WriteLine();
            Console.WriteLine("=== Phase C Conformance (modern tools) ===");

            await RunTest("tools/list carries title + annotations + icons", ToolMetadataInListing);
            await RunTest("inputSchema/outputSchema declare the 2020-12 dialect", SchemaDialectAndOutputSchemaEmitted);
            await RunTest("structured output: structuredContent + back-compat text", StructuredOutputRoundTrip);
            await RunTest("schema-validation failure returns a tool error, not a protocol error", ValidationErrorIsToolError);
            await RunTest("audio + resource_link content types round-trip", ContentTypesRoundTrip);
            await RunTest("schema validation agrees across both JSON adapters", SchemaValidationAdapterParity);

            Console.WriteLine();
            Console.WriteLine($"=== {_passed} passed, {_failed} failed ===");
            return _failed;
        }

        // -- Tests ---------------------------------------------------------------------------

        private static async Task ModernPeerHandshake()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildServer(serverEnd);
            await server.Start();

            var client = new ClientBuilder()
                .WithName("Conf Client")
                .WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(clientEnd))
                .Build();
            await client.Connect();

            Assert(client.IsConnected, "client reports connected");

            var initResult = FindInitializeResult(serverEnd.Sent);
            Assert(initResult != null, "server emitted an initialize result");
            AssertEqual(ProtocolVersion.Latest, initResult?["protocolVersion"]?.AsString(), "negotiated version is Latest");

            var tools = await client.ListTools();
            Assert(tools.Tools.Any(t => t.Name == "get-forecast"), "tools/list round-trips after handshake");
        }

        private static async Task LegacyClientNegotiation()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildServer(serverEnd);
            await server.Start();
            await clientEnd.Start();

            var request = new InitializeRequest("2024-11-05", new ClientCapabilitiesModel(), new ClientInfo("Legacy", "1.0.0"));
            var response = await clientEnd.SendRequest("initialize", request.WriteMembers);

            Assert(response.IsOk, "server responded OK to legacy initialize");
            AssertEqual("2024-11-05", response.Result?["protocolVersion"]?.AsString(), "server echoes the legacy version");
        }

        private static async Task UnsupportedClientNegotiation()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildServer(serverEnd);
            await server.Start();
            await clientEnd.Start();

            var request = new InitializeRequest("1999-01-01", new ClientCapabilitiesModel(), new ClientInfo("Weird", "1.0.0"));
            var response = await clientEnd.SendRequest("initialize", request.WriteMembers);

            Assert(response.IsOk, "server did not error on an unsupported version");
            AssertEqual(ProtocolVersion.Latest, response.Result?["protocolVersion"]?.AsString(), "server offers Latest to an unsupported client");
        }

        private static async Task ModernClientLegacyServer()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            ActAsRawServer(serverEnd, "2024-11-05");
            await serverEnd.Start();

            var client = new ClientBuilder()
                .WithName("Conf Client")
                .WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(clientEnd))
                .Build();
            await client.Connect();

            Assert(client.IsConnected, "client connects to a legacy server");

            var notifiedInitialized = Snapshot(clientEnd.Sent)
                .Any(m => Json.Parse(m)["method"]?.AsString() == "notifications/initialized");
            Assert(notifiedInitialized, "client sends 'notifications/initialized' (not 'initialized')");
        }

        private static async Task ModernClientUnsupportedServer()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            ActAsRawServer(serverEnd, "1999-01-01");
            await serverEnd.Start();

            var client = new ClientBuilder()
                .WithName("Conf Client")
                .WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(clientEnd))
                .Build();

            var threw = false;
            try
            {
                await client.Connect();
            }
            catch (ClientException)
            {
                threw = true;
            }

            Assert(threw, "client throws ClientException on an unsupported server version");
            Assert(!client.IsConnected, "client is not connected after a failed negotiation");
        }

        private static async Task StringRequestId()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildServer(serverEnd);
            await server.Start();
            await clientEnd.Start();

            const string id = "init-string-1";
            var request = new InitializeRequest(ProtocolVersion.Latest, new ClientCapabilitiesModel(), new ClientInfo("Str", "1.0.0"));
            var raw = Json.Stringify(w =>
            {
                w.Write("jsonrpc", "2.0");
                w.Write("id", id);
                w.Write("method", "initialize");
                w.Write("params", request.WriteMembers);
            });
            await clientEnd.SendRaw(raw);

            var arrived = await WaitUntil(() => Snapshot(clientEnd.Received).Any(m => IsResultForId(m, id)));
            Assert(arrived, "a response for the string id arrived");

            var responseMsg = Snapshot(clientEnd.Received).FirstOrDefault(m => IsResultForId(m, id));
            Assert(responseMsg != null, "response message located");
            if (responseMsg == null)
                return;

            var response = Json.Parse(responseMsg);
            Assert(response["id"].IsString, "echoed id is a string, not coerced to a number");
            AssertEqual(id, response["id"].AsString(), "echoed string id matches");
            AssertEqual(ProtocolVersion.Latest, response["result"].AsObject()["protocolVersion"]?.AsString(), "string-id initialize negotiated correctly");
        }

        private static async Task InitializeResultParsing()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildServer(serverEnd);
            await server.Start();
            await clientEnd.Start();

            var request = new InitializeRequest(ProtocolVersion.Latest, new ClientCapabilitiesModel(), new ClientInfo("Parse", "1.0.0"));
            var response = await clientEnd.SendRequest("initialize", request.WriteMembers);
            var parsed = new InitializeResult(response.Result);

            Assert(parsed.Capabilities != null, "capabilities are parsed (not dropped)");
            Assert(parsed.Capabilities?.Tools != null, "tools capability is parsed");
            Assert(parsed.ServerInfo != null, "serverInfo is parsed");
            AssertEqual("Conf Server", parsed.ServerInfo?.Name, "serverInfo.name parsed from lowercase 'name'");
        }

        // -- Helpers -------------------------------------------------------------------------

        private static IServer BuildServer(InMemoryTransport serverEnd)
        {
            return new ServerBuilder()
                .WithName("Conf Server")
                .WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(serverEnd))
                .WithDefaultToolsCapability(Json, tools =>
                {
                    tools.AddTool(new TestTool());
                    tools.AddTool(new StructuredTool(Json));
                })
                .Build();
        }

        /// <summary>Wires a bare transport to answer "initialize" with a fixed protocol version.</summary>
        private static void ActAsRawServer(InMemoryTransport serverEnd, string versionToReturn)
        {
            serverEnd.RequestReceived += (id, method, arguments) =>
            {
                if (method != "initialize")
                    return;

                var result = new InitializeResult(versionToReturn, new ServerCapabilitiesModel(), new ServerInfo("Raw Server", "1.0.0"));
                _ = serverEnd.SendOkResponse(id, result.WriteMembers);
            };
        }

        private static IJsonObject FindInitializeResult(List<string> messages)
        {
            foreach (var message in Snapshot(messages))
            {
                var result = Json.Parse(message)["result"]?.AsObject();
                if (result?["protocolVersion"] != null)
                    return result;
            }
            return null;
        }

        private static bool IsResultForId(string message, string id)
        {
            var obj = Json.Parse(message);
            var idProp = obj["id"];
            return obj["result"] != null && idProp != null && idProp.IsString && idProp.AsString() == id;
        }

        private static List<string> Snapshot(List<string> list)
        {
            lock (list)
                return new List<string>(list);
        }

        private static async Task<bool> WaitUntil(Func<bool> condition, int timeoutMs = 3000)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                if (condition())
                    return true;
                await Task.Delay(10);
            }
            return condition();
        }

        private static async Task RunTest(string name, Func<Task> body)
        {
            Console.WriteLine($"[{name}]");
            try
            {
                await body();
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine($"  FAIL: threw {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void Assert(bool condition, string name)
        {
            if (condition)
            {
                _passed++;
                Console.WriteLine($"  PASS: {name}");
            }
            else
            {
                _failed++;
                Console.WriteLine($"  FAIL: {name}");
            }
        }

        private static void AssertEqual(string expected, string actual, string name)
        {
            Assert(expected == actual, $"{name} (expected '{expected}', got '{actual}')");
        }
    }
}
