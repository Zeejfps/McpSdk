#nullable disable
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using McpSdk.Adapter.StreamableHttpClient;
using McpSdk.Adapter.StreamableHttpServer;
using McpSdk.Client;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Protocol.Models.ClientCapabilities;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// Phase G conformance: the Streamable HTTP transport. A real <see cref="StreamableHttpListener"/>
    /// and a real <see cref="StreamableHttpClientAdapter"/> run in-process over loopback, exercising the
    /// full initialize + tools/list + tools/call round-trip on a single endpoint, plus the protocol
    /// mechanics raw HTTP makes visible: the <c>Mcp-Session-Id</c> issued on initialize, the
    /// <c>MCP-Protocol-Version</c> header required thereafter, <c>Origin</c>→403, and unknown-session→404.
    /// </summary>
    public static partial class ConformanceTests
    {
        private static async Task StreamableHttpRoundTrip()
        {
            const string baseUrl = "http://localhost:17453";
            const string endpointPath = "/mcp";

            var listener = new StreamableHttpListener(
                baseUrl,
                endpointPath,
                Json,
                Loggers,
                onSession: async transport =>
                {
                    var server = new ServerBuilder()
                        .WithName("Http Conf Server")
                        .WithVersion("1.0.0")
                        .WithStreamableHttpTransport(transport)
                        .WithDefaultToolsCapability(Json, tools => tools.AddTool(new TestToolHandler()))
                        .Build();
                    await server.Start();
                });
            await listener.Start();

            try
            {
                using var http = new StreamableHttpClientAdapter($"{baseUrl}{endpointPath}", Loggers);
                var client = new ClientBuilder()
                    .WithName("Http Conf Client")
                    .WithVersion("1.0.0")
                    .WithStreamableHttpTransport(Json, http)
                    .Build();

                await WithTimeout(client.Connect(), 15000, "connect");
                Assert(client.IsConnected, "client connected over Streamable HTTP");

                var tools = await WithTimeout(client.ListTools(), 10000, "tools/list");
                Assert(tools.Tools.Any(t => t.Name == "get-forecast"), "tools/list round-trips over Streamable HTTP");

                var callArgs = Json.Object(w =>
                {
                    w.Write("latitude", 47.6062);
                    w.Write("longitude", -122.3321);
                    w.Write("testBool", true);
                    w.Write("testArray", new[] { "alpha", "beta" });
                });
                var result = await WithTimeout(
                    client.CallTool(new CallToolRequest("get-forecast", callArgs)), 10000, "tools/call");

                Assert(result.IsError != true, "tools/call did not report an error");
                var text = result.Content.OfType<TextContent>().FirstOrDefault()?.Text;
                Assert(text != null && text.Contains("47.6062"),
                    $"tools/call result round-trips over Streamable HTTP (got '{text}')");
            }
            finally
            {
                await listener.Stop();
            }
        }

        private static async Task StreamableHttpProtocolChecks()
        {
            const string baseUrl = "http://localhost:17454";
            const string endpointPath = "/mcp";
            const string allowedOrigin = baseUrl;
            var url = $"{baseUrl}{endpointPath}";

            var listener = new StreamableHttpListener(
                baseUrl,
                endpointPath,
                Json,
                Loggers,
                onSession: async transport =>
                {
                    var server = new ServerBuilder()
                        .WithName("Http Conf Server")
                        .WithVersion("1.0.0")
                        .WithStreamableHttpTransport(transport)
                        .WithDefaultToolsCapability(Json, tools => tools.AddTool(new TestToolHandler()))
                        .Build();
                    await server.Start();
                },
                allowedOrigins: new[] { allowedOrigin });
            await listener.Start();

            try
            {
                using var http = new HttpClient();

                // 1) initialize issues a session id and negotiates the version in the body.
                var initResp = await Post(http, url, InitializeBody(1), ("Origin", allowedOrigin));
                Assert((int)initResp.StatusCode == 200, $"initialize returns 200 (got {(int)initResp.StatusCode})");
                Assert(initResp.Headers.Contains("Mcp-Session-Id"), "initialize issues an Mcp-Session-Id header");
                var sessionId = initResp.Headers.TryGetValues("Mcp-Session-Id", out var ids) ? ids.First() : null;
                Assert(!string.IsNullOrEmpty(sessionId), "the issued session id is non-empty");
                var initBody = await initResp.Content.ReadAsStringAsync();
                AssertEqual(ProtocolVersion.Latest, Json.Parse(initBody)["result"].AsObject()["protocolVersion"]?.AsString(),
                    "initialize negotiates the latest version in the response body");

                // 2) a disallowed Origin is rejected outright (DNS-rebinding guard).
                var badOriginResp = await Post(http, url, InitializeBody(2), ("Origin", "http://evil.example"));
                Assert((int)badOriginResp.StatusCode == 403, $"a disallowed Origin is rejected with 403 (got {(int)badOriginResp.StatusCode})");

                // 3) a post-initialize request without the MCP-Protocol-Version header is a 400.
                var noVersionResp = await Post(http, url, ListToolsBody(3),
                    ("Origin", allowedOrigin), ("Mcp-Session-Id", sessionId));
                Assert((int)noVersionResp.StatusCode == 400,
                    $"a post-init request without MCP-Protocol-Version is rejected with 400 (got {(int)noVersionResp.StatusCode})");

                // 4) an unknown session id is a 404 (the client must reinitialize).
                var unknownResp = await Post(http, url, ListToolsBody(4),
                    ("Origin", allowedOrigin), ("Mcp-Session-Id", "deadbeefdeadbeef"), ("MCP-Protocol-Version", ProtocolVersion.Latest));
                Assert((int)unknownResp.StatusCode == 404,
                    $"an unknown session id is rejected with 404 (got {(int)unknownResp.StatusCode})");

                // 5) a well-formed post-init request succeeds with a JSON-RPC result.
                var listResp = await Post(http, url, ListToolsBody(5),
                    ("Origin", allowedOrigin), ("Mcp-Session-Id", sessionId), ("MCP-Protocol-Version", ProtocolVersion.Latest));
                Assert((int)listResp.StatusCode == 200, $"a valid post-init tools/list returns 200 (got {(int)listResp.StatusCode})");
                var listBody = await listResp.Content.ReadAsStringAsync();
                var listResult = new ListToolsResult(Json.Parse(listBody)["result"].AsObject());
                Assert(listResult.Tools.Any(t => t.Name == "get-forecast"), "the post-init tools/list result carries the tool");
            }
            finally
            {
                await listener.Stop();
            }
        }

        // -- Helpers -------------------------------------------------------------------------

        private static async Task<HttpResponseMessage> Post(HttpClient http, string url, string body, params (string Name, string Value)[] headers)
        {
            var message = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            foreach (var (name, value) in headers)
                message.Headers.TryAddWithoutValidation(name, value);
            return await http.SendAsync(message);
        }

        private static string InitializeBody(int id)
        {
            return Json.Stringify(w =>
            {
                w.Write("jsonrpc", "2.0");
                w.Write("id", id);
                w.Write("method", "initialize");
                w.Write("params", new InitializeRequest(ProtocolVersion.Latest, new ClientCapabilitiesModel(), new ClientInfo("Raw", "1.0.0")));
            });
        }

        private static string ListToolsBody(int id)
        {
            return Json.Stringify(w =>
            {
                w.Write("jsonrpc", "2.0");
                w.Write("id", id);
                w.Write("method", "tools/list");
                w.Write("params", Json.Object(_ => { }));
            });
        }
    }
}
