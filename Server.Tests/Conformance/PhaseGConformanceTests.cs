#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using McpSdk.Adapter.StreamableHttpClient;
using McpSdk.Adapter.StreamableHttpServer;
using McpSdk.Client;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Protocol.Models.ClientCapabilities;
using McpSdk.Protocol.Models.ServerCapabilities;
using McpSdk.Shared;

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

        private static async Task StreamableHttpServerToClient()
        {
            const string baseUrl = "http://localhost:17455";
            const string endpointPath = "/mcp";
            var url = $"{baseUrl}{endpointPath}";

            ITransport serverTransport = null;

            var listener = new StreamableHttpListener(
                baseUrl,
                endpointPath,
                Json,
                Loggers,
                onSession: transport =>
                {
                    serverTransport = transport;

                    // Answer initialize at the transport level — no full McpServer needed for this test.
                    serverTransport.RequestReceived += (id, method, args) =>
                    {
                        if (method == "initialize")
                        {
                            var result = new InitializeResult(
                                ProtocolVersion.Latest, new ServerCapabilitiesModel(), new ServerInfo("S2C Server", "1.0.0"));
                            _ = serverTransport.SendOkResponse(id, result.WriteMembers);
                        }
                    };
                    // Start the peer so it subscribes to the channel and dispatches inbound frames
                    // (what McpServer.Start would do for us in the full-server tests).
                    return serverTransport.Start();
                });
            await listener.Start();

            var clientTransport = new JsonRpcPeer(
                new HttpClientChannel(new StreamableHttpClientAdapter(url, Loggers), Json, Loggers), Json, Loggers);

            string receivedNotification = null;
            clientTransport.NotificationReceived += (method, args) => receivedNotification = method;
            // Answer a server→client request by echoing back an ok flag.
            clientTransport.RequestReceived += async (id, method, args) =>
                await clientTransport.SendOkResponse(id, w => w.Write("ok", true));

            try
            {
                await clientTransport.Start();
                var init = await WithTimeout(
                    clientTransport.SendRequest(
                        "initialize",
                        new InitializeRequest(ProtocolVersion.Latest, new ClientCapabilitiesModel(), new ClientInfo("S2C Client", "1.0.0")).WriteMembers),
                    10000, "initialize");
                Assert(init.IsOk, "transport-level initialize over Streamable HTTP succeeded");

                // Server → client notification over the SSE stream (buffered until the GET stream attaches).
                await serverTransport.SendNotification("notifications/message", w => w.Write("level", "info"));
                var gotNotification = await WaitUntil(() => receivedNotification == "notifications/message", 10000);
                Assert(gotNotification, "client received a server→client notification over the SSE stream");

                // Server → client request, answered by the client via a POST, correlated back to the server.
                var pingResult = await WithTimeout(
                    serverTransport.SendRequest("ping", w => { }), 10000, "server→client request");
                Assert(pingResult.IsOk, "server received the client's response to a server→client request");
                Assert(pingResult.Result?["ok"]?.AsBool() == true, "the server→client request round-tripped its result");
            }
            finally
            {
                await clientTransport.Stop();
                await listener.Stop();
            }
        }

        private static async Task StreamableHttpResumability()
        {
            const string baseUrl = "http://localhost:17456";
            const string endpointPath = "/mcp";
            var url = $"{baseUrl}{endpointPath}";

            ITransport serverTransport = null;

            var listener = new StreamableHttpListener(
                baseUrl,
                endpointPath,
                Json,
                Loggers,
                onSession: transport =>
                {
                    serverTransport = transport;
                    serverTransport.RequestReceived += (id, method, args) =>
                    {
                        if (method == "initialize")
                        {
                            var result = new InitializeResult(
                                ProtocolVersion.Latest, new ServerCapabilitiesModel(), new ServerInfo("Resume Server", "1.0.0"));
                            _ = serverTransport.SendOkResponse(id, result.WriteMembers);
                        }
                    };
                    return serverTransport.Start();
                });
            await listener.Start();

            try
            {
                using var http = new HttpClient();
                var initResp = await Post(http, url, InitializeBody(1));
                Assert((int)initResp.StatusCode == 200, "resume: initialize returns 200");
                var sessionId = initResp.Headers.GetValues("Mcp-Session-Id").First();

                // Queue four server→client notifications (event ids 1..4) before any stream attaches.
                for (var seq = 1; seq <= 4; seq++)
                {
                    var captured = seq;
                    await serverTransport.SendNotification("notifications/message", w => w.Write("seq", captured));
                }

                // Reconnect "after event 2": the server must replay only the missed tail (events 3 + 4).
                using var getRequest = new HttpRequestMessage(HttpMethod.Get, url);
                getRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
                getRequest.Headers.TryAddWithoutValidation("Mcp-Session-Id", sessionId);
                getRequest.Headers.TryAddWithoutValidation("MCP-Protocol-Version", ProtocolVersion.Latest);
                getRequest.Headers.TryAddWithoutValidation("Last-Event-ID", "2");

                using var getResponse = await http.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead);
                Assert((int)getResponse.StatusCode == 200, "resume: the GET stream opens with 200");

                var stream = await getResponse.Content.ReadAsStreamAsync();
                var events = await WithTimeout(ReadSseEvents(stream, 2), 10000, "resume replay");

                Assert(events.Count == 2, $"resume: replayed exactly the missed tail (got {events.Count} events)");
                AssertEqual("3", events.Count > 0 ? events[0].Id : null, "resume: first replayed event id is 3");
                AssertEqual("4", events.Count > 1 ? events[1].Id : null, "resume: second replayed event id is 4");
                var firstSeq = events.Count > 0 ? Json.Parse(events[0].Data)["params"].AsObject()["seq"].AsInt() : -1;
                Assert(firstSeq == 3, $"resume: replay starts at seq 3, skipping already-seen events (got {firstSeq})");
            }
            finally
            {
                await listener.Stop();
            }
        }

        private static async Task StreamableHttpDeleteTerminatesSession()
        {
            const string baseUrl = "http://localhost:17457";
            const string endpointPath = "/mcp";
            var url = $"{baseUrl}{endpointPath}";

            var listener = new StreamableHttpListener(
                baseUrl,
                endpointPath,
                Json,
                Loggers,
                onSession: async transport =>
                {
                    var server = new ServerBuilder()
                        .WithName("Delete Server")
                        .WithVersion("1.0.0")
                        .WithStreamableHttpTransport(transport)
                        .WithDefaultToolsCapability(Json, tools => tools.AddTool(new TestToolHandler()))
                        .Build();
                    await server.Start();
                });
            await listener.Start();

            try
            {
                using var http = new HttpClient();
                var initResp = await Post(http, url, InitializeBody(1));
                var sessionId = initResp.Headers.GetValues("Mcp-Session-Id").First();

                var before = await Post(http, url, ListToolsBody(2),
                    ("Mcp-Session-Id", sessionId), ("MCP-Protocol-Version", ProtocolVersion.Latest));
                Assert((int)before.StatusCode == 200, "delete: the session serves requests before termination");

                using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, url);
                deleteRequest.Headers.TryAddWithoutValidation("Mcp-Session-Id", sessionId);
                deleteRequest.Headers.TryAddWithoutValidation("MCP-Protocol-Version", ProtocolVersion.Latest);
                using var deleteResponse = await http.SendAsync(deleteRequest);
                Assert((int)deleteResponse.StatusCode == 200,
                    $"delete: DELETE terminates the session with 200 (got {(int)deleteResponse.StatusCode})");

                var after = await Post(http, url, ListToolsBody(3),
                    ("Mcp-Session-Id", sessionId), ("MCP-Protocol-Version", ProtocolVersion.Latest));
                Assert((int)after.StatusCode == 404,
                    $"delete: a request on the terminated session is 404 (got {(int)after.StatusCode})");
            }
            finally
            {
                await listener.Stop();
            }
        }

        // -- Helpers -------------------------------------------------------------------------

        private static async Task<List<(string Id, string Data)>> ReadSseEvents(Stream stream, int count)
        {
            var events = new List<(string, string)>();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var data = new StringBuilder();
            string id = null;

            while (events.Count < count)
            {
                var line = await reader.ReadLineAsync();
                if (line == null)
                    break;

                if (line.Length == 0)
                {
                    if (data.Length > 0)
                    {
                        events.Add((id, data.ToString()));
                        data.Clear();
                        id = null;
                    }
                    continue;
                }

                if (line[0] == ':')
                    continue;

                if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    var value = line.Substring(5).TrimStart(' ');
                    if (data.Length > 0)
                        data.Append('\n');
                    data.Append(value);
                }
                else if (line.StartsWith("id:", StringComparison.Ordinal))
                {
                    id = line.Substring(3).TrimStart(' ');
                }
            }

            return events;
        }

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
