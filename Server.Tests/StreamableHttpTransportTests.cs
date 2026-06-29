#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Adapter.StreamableHttpClient;
using McpSdk.Adapter.StreamableHttpServer;
using McpSdk.Client;
using McpSdk.Client.Transports;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Protocol.Models.ClientCapabilities;
using McpSdk.Protocol.Models.ServerCapabilities;
using McpSdk.Shared;

namespace McpSdk.Server.Tests
{
    /// <summary>
    /// The Streamable HTTP transport: a real <see cref="StreamableHttpListener"/> and
    /// <see cref="StreamableHttpClientAdapter"/> run in-process over loopback, exercising the full
    /// initialize + tools/list + tools/call round-trip on a single endpoint plus the protocol mechanics
    /// raw HTTP makes visible — the <c>Mcp-Session-Id</c>, the required <c>MCP-Protocol-Version</c> header,
    /// Origin→403, unknown-session→404, server→client traffic over the SSE stream, Last-Event-ID
    /// resumability, and DELETE session termination.
    /// </summary>
    public sealed class StreamableHttpTransportTests : ConformanceSuite
    {
        public StreamableHttpTransportTests(TestReport report) : base(report) { }

        public override string Title => "Streamable HTTP transport";

        public override async Task Run()
        {
            await Test("Streamable HTTP round-trip: initialize + tools/list + tools/call", StreamableHttpRoundTrip);
            await Test("Streamable HTTP: session id, Origin->403, version header, unknown session", StreamableHttpProtocolChecks);
            await Test("Streamable HTTP server->client: notification + request over the SSE stream", StreamableHttpServerToClient);
            await Test("Streamable HTTP resumability: Last-Event-ID replays only the missed tail", StreamableHttpResumability);
            await Test("Streamable HTTP lifecycle: DELETE terminates the session (then 404)", StreamableHttpDeleteTerminatesSession);
            await Test("Streamable HTTP per-session tools: root∪session aggregation + sibling isolation", StreamableHttpPerSessionToolAggregation);
        }

        private async Task StreamableHttpRoundTrip()
        {
            const string baseUrl = "http://localhost:17453";
            const string endpointPath = "/mcp";

            // New DI builder API (T13a): root serializer + tools, then the Streamable HTTP host. The host
            // builds one per-connection McpServer in a child scope; the root TestToolHandler is visible to it.
            var server = new ServerBuilder("Http Conf Server", "1.0.0");
            server.Context.AddNewtonsoftJson();
            server.Context.AddToolsCapability(tools => tools.AddTool(new TestToolHandler()));
            server.Context.AddStreamableHttpTransport(baseUrl, endpointPath);
            var host = server.Build();
            await host.Start();

            try
            {
                using var http = new StreamableHttpClientAdapter($"{baseUrl}{endpointPath}", Loggers);
                var clientBuilder = new ClientBuilder("Http Conf Client", "1.0.0");
                clientBuilder.Context.AddSingleton<ITransport>(new StreamableHttpTransport(http, Json, Loggers));
                var client = clientBuilder.Build();

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
                await host.Stop();
            }
        }

        private async Task StreamableHttpProtocolChecks()
        {
            const string baseUrl = "http://localhost:17454";
            const string endpointPath = "/mcp";
            const string allowedOrigin = baseUrl;
            var url = $"{baseUrl}{endpointPath}";

            // New DI builder API (T13a): the allowed origin is configured through the options so the
            // listener's Origin->403 guard still fires for a disallowed Origin.
            var server = new ServerBuilder("Http Conf Server", "1.0.0");
            server.Context.AddNewtonsoftJson();
            server.Context.AddToolsCapability(tools => tools.AddTool(new TestToolHandler()));
            server.Context.AddStreamableHttpTransport(baseUrl, endpointPath, http => http.AllowOrigin(allowedOrigin));
            var host = server.Build();
            await host.Start();

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
                await host.Stop();
            }
        }

        private async Task StreamableHttpServerToClient()
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
                    serverTransport.RequestReceived += request =>
                    {
                        if (request.Method == "initialize")
                        {
                            var result = new InitializeResult(
                                ProtocolVersion.Latest, new ServerCapabilitiesModel(), new ServerInfo("S2C Server", "1.0.0"));
                            _ = serverTransport.SendResponse(JsonRpcResponse.Ok(request.Id, result.WriteMembers));
                        }
                    };
                    // Start the transport so it dispatches inbound frames (what McpServer.Start would do
                    // for us in the full-server tests).
                    return serverTransport.Start();
                });
            await listener.Start();

            var clientTransport = new StreamableHttpTransport(
                new StreamableHttpClientAdapter(url, Loggers), Json, Loggers);

            string receivedNotification = null;
            clientTransport.NotificationReceived += notification => receivedNotification = notification.Method;
            // Answer a server→client request by echoing back an ok flag.
            clientTransport.RequestReceived += async request =>
                await clientTransport.SendResponse(JsonRpcResponse.Ok(request.Id, w => w.Write("ok", true)));

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

        private async Task StreamableHttpResumability()
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
                    serverTransport.RequestReceived += request =>
                    {
                        if (request.Method == "initialize")
                        {
                            var result = new InitializeResult(
                                ProtocolVersion.Latest, new ServerCapabilitiesModel(), new ServerInfo("Resume Server", "1.0.0"));
                            _ = serverTransport.SendResponse(JsonRpcResponse.Ok(request.Id, result.WriteMembers));
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

        private async Task StreamableHttpDeleteTerminatesSession()
        {
            const string baseUrl = "http://localhost:17457";
            const string endpointPath = "/mcp";
            var url = $"{baseUrl}{endpointPath}";

            // New DI builder API (T13a): a full per-connection McpServer over the Streamable HTTP host.
            var server = new ServerBuilder("Delete Server", "1.0.0");
            server.Context.AddNewtonsoftJson();
            server.Context.AddToolsCapability(tools => tools.AddTool(new TestToolHandler()));
            server.Context.AddStreamableHttpTransport(baseUrl, endpointPath);
            var host = server.Build();
            await host.Start();

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
                await host.Stop();
            }
        }

        /// <summary>
        /// T13b proof (implementation-plan decision #2): per-session tools <i>aggregate</i> with the shared
        /// root set rather than replacing it, and sibling sessions are isolated. One HTTP host registers a
        /// shared <c>EchoTool</c> at the root; <c>ConfigureSession</c> adds <c>CartTool</c> to every session and
        /// <c>AdminTool</c> only when the connection's <c>Origin</c> is the admin origin. Two sessions are then
        /// opened over raw HTTP with different <c>Origin</c> headers and their <c>tools/list</c> compared.
        /// </summary>
        private async Task StreamableHttpPerSessionToolAggregation()
        {
            const string baseUrl = "http://localhost:17458";
            const string endpointPath = "/mcp";
            var url = $"{baseUrl}{endpointPath}";
            const string adminOrigin = "http://admin.example";
            const string userOrigin = "http://user.example";

            // ONE host: a shared root tool (EchoTool) plus per-session tools contributed by ConfigureSession,
            // which lands its registrations in the per-connection child container (session.Context). CartTool is
            // added to every session; AdminTool only for the admin Origin. The composite's parent-then-child
            // GetServices overlay then merges root ∪ session per connection.
            var server = new ServerBuilder("Agg Server", "1.0.0");
            server.Context.AddNewtonsoftJson();
            server.Context.AddToolsCapability(tools => tools.AddTool(new EchoTool()));
            server.Context.AddStreamableHttpTransport(baseUrl, endpointPath, http => http.ConfigureSession(session =>
            {
                session.Context.AddToolsCapability(t => t.AddTool(new CartTool()));
                if (session.Origin == adminOrigin)
                    session.Context.AddToolsCapability(t => t.AddTool(new AdminTool()));
            }));
            var host = server.Build();
            await host.Start();

            try
            {
                using var http = new HttpClient();

                // Session A from the ADMIN origin, session B from a NON-admin origin (concurrent siblings).
                var toolsA = await ListToolsForSession(http, url, adminOrigin);
                var toolsB = await ListToolsForSession(http, url, userOrigin);

                // (3) BOTH sessions see the shared EchoTool (overlay = root ∪ session).
                Assert(toolsA.Contains("echo"), "aggregation: admin session sees the shared root EchoTool");
                Assert(toolsB.Contains("echo"), "aggregation: non-admin session sees the shared root EchoTool");

                // (1) admin session aggregates EchoTool + CartTool + AdminTool.
                Assert(toolsA.Contains("cart"), "aggregation: admin session sees its per-session CartTool");
                Assert(toolsA.Contains("admin"), "aggregation: admin session sees the admin-origin-only AdminTool");

                // (2) non-admin session sees EchoTool + CartTool but NOT AdminTool — the admin-only tool is
                //     unique to session A's child scope, so its absence here proves sibling isolation.
                Assert(toolsB.Contains("cart"), "aggregation: non-admin session sees its per-session CartTool");
                Assert(!toolsB.Contains("admin"),
                    "isolation: non-admin session does NOT see session A's admin-only AdminTool (sibling isolation)");
            }
            finally
            {
                await host.Stop();
            }
        }

        /// <summary>Opens one session from <paramref name="origin"/> and returns its <c>tools/list</c> tool names.</summary>
        private async Task<List<string>> ListToolsForSession(HttpClient http, string url, string origin)
        {
            var initResp = await Post(http, url, InitializeBody(1), ("Origin", origin));
            var sessionId = initResp.Headers.GetValues("Mcp-Session-Id").First();

            var listResp = await Post(http, url, ListToolsBody(2),
                ("Origin", origin), ("Mcp-Session-Id", sessionId), ("MCP-Protocol-Version", ProtocolVersion.Latest));
            var listBody = await listResp.Content.ReadAsStringAsync();
            var listResult = new ListToolsResult(Json.Parse(listBody)["result"].AsObject());
            return listResult.Tools.Select(t => t.Name).ToList();
        }

        // Minimal tool handlers for the aggregation test: distinct names, trivial results.
        private sealed class EchoTool : IToolHandler
        {
            public Tool Tool { get; } = new Tool("echo", "Shared root echo tool", new ObjectSchema());
            public Task<CallToolResult> Call(IJsonObject arguments, McpRequestContext context)
                => Task.FromResult(new CallToolResult([new TextContent("echo")], false));
        }

        private sealed class CartTool : IToolHandler
        {
            public Tool Tool { get; } = new Tool("cart", "Per-session cart tool", new ObjectSchema());
            public Task<CallToolResult> Call(IJsonObject arguments, McpRequestContext context)
                => Task.FromResult(new CallToolResult([new TextContent("cart")], false));
        }

        private sealed class AdminTool : IToolHandler
        {
            public Tool Tool { get; } = new Tool("admin", "Admin-origin-only tool", new ObjectSchema());
            public Task<CallToolResult> Call(IJsonObject arguments, McpRequestContext context)
                => Task.FromResult(new CallToolResult([new TextContent("admin")], false));
        }

        // -- Helpers -------------------------------------------------------------------------

        private async Task<List<(string Id, string Data)>> ReadSseEvents(Stream stream, int count)
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

        private async Task<HttpResponseMessage> Post(HttpClient http, string url, string body, params (string Name, string Value)[] headers)
        {
            var message = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            foreach (var (name, value) in headers)
                message.Headers.TryAddWithoutValidation(name, value);
            return await http.SendAsync(message);
        }

        private string InitializeBody(int id)
        {
            return Json.Stringify(w =>
            {
                w.Write("jsonrpc", "2.0");
                w.Write("id", id);
                w.Write("method", "initialize");
                w.Write("params", new InitializeRequest(ProtocolVersion.Latest, new ClientCapabilitiesModel(), new ClientInfo("Raw", "1.0.0")));
            });
        }

        private string ListToolsBody(int id)
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
